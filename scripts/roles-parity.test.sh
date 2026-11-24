#!/usr/bin/env bash
#
# Tests de scripts/roles-parity.sh. Corre en scripts/check.sh y en ci.yml, junto
# al chequeo mismo: un guardia sin prueba de que enrojece es un guardia dormido.
#
# Cada caso arma una copia temporal del realm, del catalogo o de un archivo de
# politicas, rompe una sola cosa y afirma el codigo de salida y que el mensaje
# nombra el rol culpable.
#
# Uso:
#   scripts/roles-parity.test.sh

set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
parity="$repo_root/scripts/roles-parity.sh"
realm="$repo_root/infra/keycloak/sorteo360-realm.json"
doc="$repo_root/docs/roles.md"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

passed=0
failed=0
last_output=""

# Afirma el codigo de salida de una corrida. Guarda la salida en $last_output.
expect() {
  local name="$1" want="$2"
  shift 2
  local output status
  output="$("$parity" "$@" 2>&1)"
  status=$?
  last_output="$output"
  if [ "$status" -ne "$want" ]; then
    echo "FALLO: $name — esperaba salida $want, dio $status" >&2
    echo "$output" | sed 's/^/       /' >&2
    failed=$((failed + 1))
    return 1
  fi
  passed=$((passed + 1))
  echo "ok: $name"
}

# Afirma que el ultimo $last_output contiene un texto.
expect_output() {
  local name="$1" needle="$2"
  if ! printf '%s' "$last_output" | grep -qF -- "$needle"; then
    echo "FALLO: $name — la salida no menciona '$needle'" >&2
    echo "$last_output" | sed 's/^/       /' >&2
    failed=$((failed + 1))
    return 1
  fi
  passed=$((passed + 1))
  echo "ok: $name"
}

# Copia los archivos de politicas del repo a un arbol falso con la misma forma
# (<servicio>/<proyecto>/Auth/AuthorizationPolicies.cs), para poder romper uno.
fake_root="$tmp/repo"
for f in "$repo_root"/*/*/Auth/AuthorizationPolicies.cs; do
  rel="${f#"$repo_root"/}"
  mkdir -p "$fake_root/$(dirname "$rel")"
  cp "$f" "$fake_root/$rel"
done

# --- 1. El repo real pasa ---------------------------------------------------
expect "el realm, el catalogo y las politicas del repo coinciden" 0

# --- 2. Una fila borrada del catalogo enrojece, nombrando el rol -------------
grep -vF '| `events:cancel` |' "$doc" > "$tmp/doc-sin-fila.md"
expect "falta la fila de events:cancel en el catalogo → rojo" 1 "$realm" "$tmp/doc-sin-fila.md" "$repo_root"
expect_output "el fallo nombra el rol que falta en el catalogo" "events:cancel"

# --- 3. Un rol del catalogo que el realm no tiene enrojece ----------------
jq '.roles.realm |= map(select(.name != "raffles:draw"))
    | .roles.realm |= map(if .composites.realm then .composites.realm -= ["raffles:draw"] else . end)' \
  "$realm" > "$tmp/realm-sin-rol.json"
expect "el catalogo lista raffles:draw y el realm no → rojo" 1 "$tmp/realm-sin-rol.json" "$doc" "$fake_root"
expect_output "el fallo nombra el rol que sobra en el catalogo" "raffles:draw"

# --- 4. Un compuesto que difiere enrojece ----------------------------------
jq '.roles.realm |= map(if .name == "opt_coordinator" then .composites.realm += ["events:cancel"] else . end)' \
  "$realm" > "$tmp/realm-compuesto.json"
expect "opt_coordinator gana events:cancel solo en el realm → rojo" 1 "$tmp/realm-compuesto.json" "$doc" "$repo_root"
expect_output "el fallo nombra el rol cuyo compuesto difiere" "events:cancel"
expect_output "el fallo nombra el compuesto" "opt_coordinator"

# --- 5. Una cuenta de servicio que difiere enrojece --------------------------
jq '.users |= map(if .serviceAccountClientId == "events-service" then .realmRoles += ["notifications:read"] else . end)' \
  "$realm" > "$tmp/realm-cuenta.json"
expect "events-service gana notifications:read solo en el realm → rojo" 1 "$tmp/realm-cuenta.json" "$doc" "$repo_root"
expect_output "el fallo nombra el rol cuya cuenta de servicio difiere" "notifications:read"

# --- 6. Una constante de politica sin rol en el realm enrojece -------------
events_matches=("$fake_root"/events-service/*/Auth/AuthorizationPolicies.cs)
events_policies="${events_matches[0]}"
cp "$events_policies" "$tmp/events-original.cs"
sed 's/"events:cancel"/"events:list"/' "$tmp/events-original.cs" > "$events_policies"
expect "una politica pide events:list, que el realm no tiene → rojo" 1 "$realm" "$doc" "$fake_root"
expect_output "el fallo nombra la constante sin rol" "events:list"
expect_output "el fallo nombra el rol sin constante" "events:cancel"
cp "$tmp/events-original.cs" "$events_policies"

# --- 7. La seccion Planned no se compara ------------------------------------
expect "el arbol falso restaurado vuelve a verde" 0 "$realm" "$doc" "$fake_root"
# Si la seccion o la fila desaparecen, este caso ya no prueba nada: se exige que esten.
if grep -q '^## Planned' "$doc" && grep -qF '| `platform:staff` |' "$doc"; then
  expect "platform:staff en Planned no se exige en el realm" 0 "$realm" "$doc" "$repo_root"
else
  echo "FALLO: docs/roles.md ya no tiene platform:staff bajo ## Planned; el caso no prueba nada" >&2
  failed=$((failed + 1))
fi

# --- 8. Una fila repetida, un Kind invalido o uno que no cuadra son error de formato
awk '{ print } /^\| `events:cancel` \|/ { print }' "$doc" > "$tmp/doc-repetida.md"
expect "events:cancel aparece dos veces → error" 2 "$realm" "$tmp/doc-repetida.md" "$repo_root"
expect_output "el error nombra la fila repetida" "events:cancel"

sed 's/^| `events:cancel` | realm role |/| `events:cancel` | realm rol |/' "$doc" > "$tmp/doc-kind.md"
expect "Kind mal escrito → error" 2 "$realm" "$tmp/doc-kind.md" "$repo_root"
expect_output "el error nombra el rol del Kind invalido" "events:cancel"

sed 's/^| `notifications:send` | service-only realm role |/| `notifications:send` | realm role |/' "$doc" > "$tmp/doc-servicio.md"
expect "rol solo de servicio marcado como realm role → error" 2 "$realm" "$tmp/doc-servicio.md" "$repo_root"
expect_output "el error nombra el rol" "notifications:send"

# --- 9. Un catalogo que el parser no entiende NO pasa en verde vacio -------
printf '# Roles\n\nnada que leer\n' > "$tmp/doc-vacio.md"
expect "catalogo sin filas legibles → error, no verde" 2 "$realm" "$tmp/doc-vacio.md" "$repo_root"

echo
echo "roles-parity.test.sh: $passed ok, $failed fallos"
[ "$failed" -eq 0 ]
