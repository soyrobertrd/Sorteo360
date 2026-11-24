#!/bin/sh
# ===========================================================================
# Un login de PostgreSQL por servicio.
#
# Lo corre el contenedor `postgres-roles` en CADA despliegue, después de que
# postgres está sano y antes de que arranque cualquier servicio. Es idempotente:
# crea lo que falta, corrige la propiedad y vuelve a fijar las claves desde el
# entorno, y sale. Correrlo dos veces no cambia nada.
#
# No vive en infra/postgres/ a propósito: todo lo de esa carpeta se monta en
# /docker-entrypoint-initdb.d y correría también en el primer arranque, y
# init.sh solo corre con el volumen vacío, cuando dev y stage ya tienen datos.
#
# Se conecta como el superusuario (PGUSER/PGPASSWORD, el par POSTGRES_*), que se
# queda para administración y para estos contenedores. Ese usuario conserva
# SUPERUSER: un superusuario ignora la propiedad, así que volver a apuntar un
# servicio a él funciona sea quien sea el dueño de las tablas.
#
# NUNCA `REASSIGN OWNED BY`: también mueve objetos compartidos, y corrido dentro
# de una base le daría al login cada base de datos del superusuario. La propiedad
# se mueve objeto por objeto, recorriendo el catálogo.
#
# Servicio nuevo: una línea más en la lista del final, con su base, su login y la
# variable de su clave (ver docs/adding-a-new-service.md).
# ===========================================================================
set -eu

# Asegura un login dueño de su base y de todo lo que hay adentro.
#   $1 base de datos   $2 login   $3 NOMBRE de la variable con la clave
ensure_login() {
  db="$1"
  login="$2"
  password_var="$3"
  eval "password=\${$password_var}"

  # La clave viaja como variable de psql (:'password') y nunca dentro del texto
  # del SQL: psql la cita como literal, así que ninguna clave rompe la sentencia.
  psql -X -q -v ON_ERROR_STOP=1 -d postgres \
    -v db="$db" -v login="$login" -v password="$password" <<'SQL'
SELECT format('CREATE ROLE %I LOGIN', :'login')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'login') \gexec

-- Los atributos se fijan en cada corrida: si alguien le subió privilegios a mano,
-- el próximo despliegue los vuelve a bajar.
ALTER ROLE :"login" WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD :'password';

-- init.sh ya la crea en un volumen nuevo; acá es por si falta.
SELECT format('CREATE DATABASE %I', :'db')
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'db') \gexec

ALTER DATABASE :"db" OWNER TO :"login";
REVOKE CONNECT ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db" TO :"login";
SQL

  # Dentro de la base: todo lo que es del superusuario pasa al login. `public` no
  # aparece porque desde PostgreSQL 15 es de pg_database_owner, que ya es el login.
  # Las tablas van primero porque arrastran sus índices y sus secuencias propias
  # (identity/serial); cambiar el dueño de esas secuencias por separado falla.
  psql -X -q -v ON_ERROR_STOP=1 -d "$db" -v login="$login" <<'SQL'
SELECT format('ALTER SCHEMA %I OWNER TO %I', n.nspname, :'login')
FROM pg_namespace n
WHERE n.nspowner = (SELECT oid FROM pg_roles WHERE rolname = current_user)
  AND n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
  AND NOT EXISTS (SELECT 1 FROM pg_depend d
                  WHERE d.classid = 'pg_namespace'::regclass AND d.objid = n.oid AND d.deptype = 'e')
\gexec

SELECT format('ALTER TABLE %I.%I OWNER TO %I', n.nspname, c.relname, :'login')
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('r', 'p', 'v', 'm', 'f')
  AND c.relowner = (SELECT oid FROM pg_roles WHERE rolname = current_user)
  AND n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
  AND NOT EXISTS (SELECT 1 FROM pg_depend d
                  WHERE d.classid = 'pg_class'::regclass AND d.objid = c.oid AND d.deptype = 'e')
\gexec

-- Las secuencias sueltas: las propias de una tabla ya cambiaron arriba.
SELECT format('ALTER SEQUENCE %I.%I OWNER TO %I', n.nspname, c.relname, :'login')
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'S'
  AND c.relowner = (SELECT oid FROM pg_roles WHERE rolname = current_user)
  AND n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
  AND NOT EXISTS (SELECT 1 FROM pg_depend d
                  WHERE d.classid = 'pg_class'::regclass AND d.objid = c.oid AND d.deptype = 'e')
\gexec

SELECT format('ALTER ROUTINE %s OWNER TO %I', p.oid::regprocedure, :'login')
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE p.prokind <> 'a'
  AND p.proowner = (SELECT oid FROM pg_roles WHERE rolname = current_user)
  AND n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
  AND NOT EXISTS (SELECT 1 FROM pg_depend d
                  WHERE d.classid = 'pg_proc'::regclass AND d.objid = p.oid AND d.deptype = 'e')
\gexec

-- Enums, dominios y rangos: una migración que les agrega un valor necesita ser su dueña.
SELECT format('ALTER TYPE %s OWNER TO %I', t.oid::regtype, :'login')
FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace
WHERE t.typtype IN ('e', 'd', 'r')
  AND t.typowner = (SELECT oid FROM pg_roles WHERE rolname = current_user)
  AND n.nspname !~ '^pg_' AND n.nspname <> 'information_schema'
  AND NOT EXISTS (SELECT 1 FROM pg_depend d
                  WHERE d.classid = 'pg_type'::regclass AND d.objid = t.oid AND d.deptype = 'e')
\gexec
SQL

  echo "postgres-roles: $login es dueño de $db"
}

# Base de datos, login y variable con la clave, de a tres.
set -- \
  events        events_app        EVENTS_DB_PASSWORD \
  convocatorias convocatorias_app CONVOCATORIAS_DB_PASSWORD \
  notifications notifications_app NOTIFICATIONS_DB_PASSWORD \
  keycloak      keycloak_app      KEYCLOAK_DB_PASSWORD

# Todas las claves se revisan antes de tocar nada: una que falta no deja la mitad
# de los logins cambiados.
missing=0
i=1
while [ "$i" -le "$#" ]; do
  eval "login=\${$((i + 1))} password_var=\${$((i + 2))}"
  eval "password=\${$password_var:-}"
  if [ -z "$password" ]; then
    echo "postgres-roles: falta $password_var, la clave del login $login" >&2
    missing=1
  fi
  i=$((i + 3))
done
[ "$missing" -eq 0 ] || exit 1

while [ "$#" -gt 0 ]; do
  ensure_login "$1" "$2" "$3"
  shift 3
done

# Un certificado que postgres-certs renovó en este despliegue no lo lee un postgres que
# ya estaba corriendo y no se recreó: recargar la configuración lo carga.
psql -X -q -v ON_ERROR_STOP=1 -d postgres -c 'SELECT pg_reload_conf()' >/dev/null
echo "postgres-roles: configuración de postgres recargada"
