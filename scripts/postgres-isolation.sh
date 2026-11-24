#!/usr/bin/env bash
#
# Aislamiento de los logins de PostgreSQL de cada servicio, contra un
# stack VIVO. Lo que deja infra/postgres-setup/roles.sh, comprobado desde afuera:
#
#   - cada login entra a su propia base con su clave, y una clave equivocada no
#     entra. Se conecta a la IP del contenedor en su red, nunca al socket local ni a
#     127.0.0.1: la imagen oficial confía en los dos sin pedir clave, así que por ahí
#     cualquier clave pasaría;
#   - cada login es rechazado en la base de cada otro servicio;
#   - ningún login es superusuario ni puede crear bases o roles;
#   - todo lo que hay en cada base (tablas, vistas, secuencias, funciones, tipos y
#     esquemas) es del login de esa base;
#   - por red solo se entra con TLS: la misma conexión sin cifrar se rechaza,
#     y cada servicio conectado lo está cifrado.
#
# No corre en scripts/check.sh ni en CI: necesita el stack levantado.
#
# Uso:
#   scripts/postgres-isolation.sh
#   POSTGRES_CONTAINER=<contenedor> scripts/postgres-isolation.sh   # otro entorno
#
# Las claves salen de EVENTS_DB_PASSWORD, CONVOCATORIAS_DB_PASSWORD, ... (o de .env); sin
# ellas, el valor local `localdev`. El superusuario sale de POSTGRES_USER.

set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Lee una variable del entorno, o de .env si no está, o el default.
setting() {
  local name="$1" default="$2" value="${!1:-}"
  if [ -z "$value" ] && [ -f "$repo_root/.env" ]; then
    value="$(grep -E "^$name=" "$repo_root/.env" | tail -n1 | cut -d= -f2-)"
  fi
  echo "${value:-$default}"
}

container="${POSTGRES_CONTAINER:-$(cd "$repo_root" && docker compose ps -q postgres 2>/dev/null)}"
if [ -z "$container" ]; then
  echo "postgres-isolation: postgres no está corriendo (o pasar POSTGRES_CONTAINER)" >&2
  exit 2
fi

admin="$(setting POSTGRES_USER sorteo360)"

# La IP del contenedor en la red de Compose: la regla `hostssl all all all scram-sha-256`.
address="$(docker exec "$container" hostname -i | awk '{print $1}')"
if [ -z "$address" ]; then
  echo "postgres-isolation: no se pudo leer la IP del contenedor de postgres" >&2
  exit 2
fi

# Arreglos paralelos y no asociativos: el bash de macOS es 3.2.
dbs=(events convocatorias notifications keycloak)
logins=(events_app convocatorias_app notifications_app keycloak_app)
passwords=(
  "$(setting EVENTS_DB_PASSWORD localdev)"
  "$(setting CONVOCATORIAS_DB_PASSWORD localdev)"
  "$(setting NOTIFICATIONS_DB_PASSWORD localdev)"
  "$(setting KEYCLOAK_DB_PASSWORD localdev)"
)

failures=0
fail() {
  echo "  FALLO: $*" >&2
  failures=$((failures + 1))
}

# Conecta como un login, con clave, a una base. Devuelve el código de psql. Por TLS sin
# verificar el nombre: se conecta a una IP, y el certificado es para DNS:postgres. Que el
# nombre se verifique lo prueban los servicios, que usan verify-full.
connect_as() {
  local login="$1" password="$2" db="$3" sslmode="${4:-require}"
  docker exec -e PGPASSWORD="$password" -e PGSSLMODE="$sslmode" "$container" \
    psql -X -q -At -h "$address" -U "$login" -d "$db" -c 'select 1' >/dev/null 2>&1
}

# Consulta como el superusuario por el socket local.
admin_query() {
  docker exec "$container" psql -X -q -At -U "$admin" -d "$1" -v ON_ERROR_STOP=1 -c "$2"
}

echo "== cada login entra a su base y a ninguna otra"
for i in "${!dbs[@]}"; do
  db="${dbs[$i]}"
  login="${logins[$i]}"
  password="${passwords[$i]}"
  if connect_as "$login" "$password" "$db"; then
    echo "  ok: $login entra a $db"
  else
    fail "$login no puede entrar a $db"
  fi
  if connect_as "$login" "no-es-la-clave" "$db"; then
    fail "$login entra a $db con una clave equivocada: la conexión no pide clave"
  fi
  if connect_as "$login" "$password" "$db" disable; then
    fail "$login entra a $db sin TLS"
  fi
  for other in "${dbs[@]}"; do
    [ "$other" = "$db" ] && continue
    if connect_as "$login" "$password" "$other"; then
      fail "$login entra a $other, que no es suya"
    fi
  done
done

echo "== ningún login es superusuario ni crea bases o roles"
login_list="$(printf "'%s'," "${logins[@]}")"
privileged="$(admin_query postgres \
  "select rolname from pg_roles where rolname in (${login_list%,}) and (rolsuper or rolcreatedb or rolcreaterole)")" \
  || fail "no se pudo consultar pg_roles"
if [ -n "$privileged" ]; then
  fail "logins con privilegios de más: $(echo "$privileged" | tr '\n' ' ')"
else
  echo "  ok"
fi
existing="$(admin_query postgres "select count(*) from pg_roles where rolname in (${login_list%,})")" \
  || fail "no se pudo consultar pg_roles"
[ "$existing" = "${#dbs[@]}" ] || fail "existen $existing de ${#dbs[@]} logins"

echo "== todo lo de cada base es de su login"
for i in "${!dbs[@]}"; do
  db="${dbs[$i]}"
  login="${logins[$i]}"
  foreign="$(admin_query "$db" "
    with owned as (
      select 'esquema ' || nspname as obj, nspowner as owner from pg_namespace
        where nspname !~ '^pg_' and nspname <> 'information_schema' and nspname <> 'public'
      union all
      select 'relación ' || n.nspname || '.' || c.relname, c.relowner
        from pg_class c join pg_namespace n on n.oid = c.relnamespace
        where c.relkind in ('r', 'p', 'v', 'm', 'f', 'S')
          and n.nspname !~ '^pg_' and n.nspname <> 'information_schema'
      union all
      select 'función ' || p.oid::regprocedure, p.proowner
        from pg_proc p join pg_namespace n on n.oid = p.pronamespace
        where p.prokind <> 'a'
          and n.nspname !~ '^pg_' and n.nspname <> 'information_schema'
      union all
      select 'tipo ' || t.oid::regtype, t.typowner
        from pg_type t join pg_namespace n on n.oid = t.typnamespace
        where t.typtype in ('e', 'd', 'r')
          and n.nspname !~ '^pg_' and n.nspname <> 'information_schema'
    )
    select obj || ' es de ' || pg_get_userbyid(owner) from owned
    where pg_get_userbyid(owner) <> '$login'")" \
    || { fail "no se pudo consultar $db"; continue; }
  tables="$(admin_query "$db" "select count(*) from pg_class c join pg_namespace n on n.oid = c.relnamespace where c.relkind = 'r' and n.nspname !~ '^pg_' and n.nspname <> 'information_schema'")"
  if [ -n "$foreign" ]; then
    fail "$db tiene objetos que no son de $login:"
    echo "$foreign" | sed 's/^/    /' >&2
  else
    echo "  ok: $db ($tables tablas) es toda de $login"
  fi
done

echo "== cada servicio conectado lo está por TLS"
connected="$(admin_query postgres "
  select a.usename || ' ' || s.ssl from pg_stat_ssl s join pg_stat_activity a using (pid)
  where a.backend_type = 'client backend' and a.usename in (${login_list%,})")" \
  || fail "no se pudo consultar pg_stat_ssl"
if [ -z "$connected" ]; then
  fail "ningún servicio está conectado: ¿está el stack arriba?"
elif echo "$connected" | grep -q ' false$'; then
  fail "conexiones sin TLS: $(echo "$connected" | grep ' false$' | cut -d' ' -f1 | sort -u | tr '\n' ' ')"
else
  echo "  ok: $(echo "$connected" | cut -d' ' -f1 | sort -u | tr '\n' ' ')"
fi

if [ "$failures" -gt 0 ]; then
  echo "postgres-isolation: $failures fallo(s)" >&2
  exit 1
fi
echo "postgres-isolation: OK"
