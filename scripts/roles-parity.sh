#!/usr/bin/env bash
#
# Paridad de roles entre el realm JSON (infra/keycloak/sorteo360-realm.json), el
# catalogo (docs/roles.md) y las constantes de las politicas de cada servicio
# (*/*/Auth/AuthorizationPolicies.cs).
#
# Un rol nuevo aterriza en varios lugares a la vez, y ningun test se entera si uno
# se queda atras: el catalogo miente en silencio, o una politica exige un rol que el
# realm no tiene y la ruta responde 403 a todo el mundo. Este chequeo falla, nombrando
# el rol, cuando:
#   - un rol del realm falta en el catalogo, o el catalogo lista uno que el realm no tiene;
#   - el catalogo dice que un rol es compuesto y el realm no, o al reves;
#   - la columna "Granted by job" de un rol difiere de los compuestos que lo contienen;
#   - la columna "Service accounts holding it" difiere de las cuentas de servicio que lo llevan;
#   - las constantes "noun:verb" de las politicas difieren de los roles no compuestos del realm.
#
# No compara descripciones (el realm las tiene en espanol, el catalogo en ingles), ni el
# frontend, que puede ir atrasado. La seccion "## Planned" del catalogo no se compara.
#
# Uso:
#   scripts/roles-parity.sh
#   scripts/roles-parity.sh <realm.json> <roles.md> <raiz-del-repo>   # para los tests

set -uo pipefail

# El orden de `sort` tiene que ser el mismo que espera `comm`.
export LC_ALL=C

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

realm_file="${1:-$repo_root/infra/keycloak/sorteo360-realm.json}"
doc_file="${2:-$repo_root/docs/roles.md}"
policies_root="${3:-$repo_root}"

for f in "$realm_file" "$doc_file"; do
  if [ ! -f "$f" ]; then
    echo "roles-parity: no existe '$f'" >&2
    exit 2
  fi
done

if ! command -v jq > /dev/null; then
  echo "roles-parity: hace falta jq" >&2
  exit 2
fi

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

# --- Lectura del realm ------------------------------------------------------
# Una linea por rol: "rol<TAB>tipo<TAB>compuestos<TAB>cuentas", con tipo `composite`
# o `role`, y las dos listas ordenadas y separadas por espacio, o `none` si no hay. Nunca
# un campo vacio: `read` con IFS de tabulador junta dos tabuladores seguidos en uno.
# Los compuestos no llevan listas: lo que conceden se compara desde el lado de cada rol.
jq -r '
  .roles.realm as $roles
  | [.users[]? | select(.serviceAccountClientId)] as $accounts
  | $roles[]
  | if .composite then
      [.name, "composite", "none", "none"]
    else
      .name as $r
      | [ $r, "role",
          ([$roles[] | select(.composite) | select((.composites.realm // []) | index($r)) | .name] | sort | join(" ") | if . == "" then "none" else . end),
          ([$accounts[] | select((.realmRoles // []) | index($r)) | .serviceAccountClientId] | sort | join(" ") | if . == "" then "none" else . end)
        ]
    end
  | @tsv
' "$realm_file" | sort > "$tmp/realm.tsv" || {
  echo "roles-parity: no se pudo leer '$realm_file' con jq" >&2
  exit 2
}

# --- Lectura del catalogo ---------------------------------------------------
# Las filas que importan empiezan con "| `rol` |". Hasta "## Planned" y nada despues.
# Celdas: 1 rol, 2 tipo; en las de rol, 5 compuestos y 6 cuentas, cada nombre entre
# backticks o la palabra `none`. Una fila de rol con otra cantidad de celdas es un error
# de formato, no una diferencia.
awk -F'|' -v doc="$doc_file" '
  function trim(s) { gsub(/^[ \t]+|[ \t]+$/, "", s); return s }
  # Los nombres entre backticks de una celda, ordenados y separados por espacio.
  function names(cell,    out, n, i, j, t, arr) {
    n = 0
    while (match(cell, /`[^`]+`/)) {
      arr[++n] = substr(cell, RSTART + 1, RLENGTH - 2)
      cell = substr(cell, RSTART + RLENGTH)
    }
    for (i = 2; i <= n; i++) {
      t = arr[i]
      for (j = i - 1; j >= 1 && arr[j] > t; j--) arr[j + 1] = arr[j]
      arr[j + 1] = t
    }
    out = ""
    for (i = 1; i <= n; i++) out = out (i > 1 ? " " : "") arr[i]
    return out == "" ? "none" : out
  }
  /^## Planned/ { exit }
  /^\| `/ {
    role = names($2)
    kind = trim($3)
    if (seen[role]++) {
      print "roles-parity: " doc ":" NR ": `" role "` aparece en mas de una fila" > "/dev/stderr"
      bad = 1
      next
    }
    if (kind == "realm composite") { print role "\tcomposite\tnone\tnone"; next }
    if (kind != "realm role" && kind != "service-only realm role") {
      print "roles-parity: " doc ":" NR ": `" role "` tiene Kind \"" kind "\"; se esperaba realm role, service-only realm role o realm composite" > "/dev/stderr"
      bad = 1
      next
    }
    if (NF != 9) {
      print "roles-parity: " doc ":" NR ": la fila de `" role "` tiene " NF - 2 " celdas, se esperaban 7" > "/dev/stderr"
      bad = 1
      next
    }
    comps = names($6)
    # Un rol solo de servicio es justamente el que ningun compuesto lleva, y al reves.
    if ((kind == "service-only realm role") != (comps == "none")) {
      print "roles-parity: " doc ":" NR ": `" role "` tiene Kind \"" kind "\" y Granted by job [" comps "]; un service-only realm role es el que ningun compuesto lleva" > "/dev/stderr"
      bad = 1
    }
    print role "\trole\t" comps "\t" names($7)
  }
  END { if (bad) exit 3 }
' "$doc_file" | sort > "$tmp/doc.tsv"
doc_status="${PIPESTATUS[0]}"

if [ "$doc_status" -ne 0 ]; then
  exit 2
fi

# Red de seguridad: un catalogo reformateado que el parser ya no entiende pasaria en
# verde vacio. Sin filas no hay nada que comparar, y eso es un error.
if [ ! -s "$tmp/doc.tsv" ]; then
  echo "roles-parity: no se leyo ninguna fila de rol en $doc_file — el parser no entiende el formato" >&2
  exit 2
fi

# --- Lectura de las politicas ------------------------------------------------
# Toda cadena "noun:verb" de los archivos de politicas. Los nombres de politica que no
# son roles (CRM `reveal-bank-accounts`) no llevan dos puntos y no entran.
policy_files=("$policies_root"/*/*/Auth/AuthorizationPolicies.cs)
if [ ! -f "${policy_files[0]}" ]; then
  echo "roles-parity: no hay ningun AuthorizationPolicies.cs bajo $policies_root" >&2
  exit 2
fi
grep -ohE '"([a-z][a-z-]*(:[a-z][a-z-]*)+|platform-admin)"' "${policy_files[@]}" | tr -d '"' | sort -u > "$tmp/policies.txt"

# --- Comparacion ------------------------------------------------------------
findings=0
report() {
  echo "  $1"
  findings=$((findings + 1))
}

cut -f1 "$tmp/realm.tsv" | sort -u > "$tmp/realm-names.txt"
cut -f1 "$tmp/doc.tsv" | sort -u > "$tmp/doc-names.txt"

while read -r role; do
  report "$role: esta en el realm y falta en el catalogo"
done < <(comm -23 "$tmp/realm-names.txt" "$tmp/doc-names.txt")

while read -r role; do
  report "$role: esta en el catalogo y falta en el realm"
done < <(comm -13 "$tmp/realm-names.txt" "$tmp/doc-names.txt")

# Los roles que estan de los dos lados, campo por campo.
join -t $'\t' "$tmp/realm.tsv" "$tmp/doc.tsv" > "$tmp/both.tsv"
while IFS=$'\t' read -r role r_kind r_comp r_acct d_kind d_comp d_acct; do
  if [ "$r_kind" != "$d_kind" ]; then
    report "$role: el realm dice '$r_kind' y el catalogo '$d_kind'"
    continue
  fi
  [ "$r_kind" = "role" ] || continue
  if [ "$r_comp" != "$d_comp" ]; then
    report "$role: compuestos en el realm [$r_comp], en el catalogo [$d_comp]"
  fi
  if [ "$r_acct" != "$d_acct" ]; then
    report "$role: cuentas de servicio en el realm [$r_acct], en el catalogo [$d_acct]"
  fi
done < "$tmp/both.tsv"

awk -F'\t' '$2 == "role" { print $1 }' "$tmp/realm.tsv" | sort -u > "$tmp/realm-functions.txt"

while read -r role; do
  report "$role: es un rol del realm y ninguna AuthorizationPolicies.cs lo declara"
done < <(comm -23 "$tmp/realm-functions.txt" "$tmp/policies.txt")

while read -r role; do
  report "$role: lo declara una AuthorizationPolicies.cs y el realm no lo tiene"
done < <(comm -13 "$tmp/realm-functions.txt" "$tmp/policies.txt")

if [ "$findings" -gt 0 ]; then
  echo "roles-parity: $findings diferencia(s) entre el realm, docs/roles.md y las politicas" >&2
  echo "Un rol cambia en varios lugares a la vez: ver el encabezado de docs/roles.md." >&2
  exit 1
fi

echo "roles-parity: OK — $(wc -l < "$tmp/realm-names.txt" | tr -d ' ') roles coinciden entre el realm, docs/roles.md y las politicas"
