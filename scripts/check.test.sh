#!/usr/bin/env bash
#
# Tests de scripts/check.sh. Corre en scripts/check.sh y en ci.yml: un guardia
# sin prueba de que enrojece es un guardia dormido.
#
# Cada caso arma un repo falso con una copia de check.sh y un frontend/, y pone
# un npm de mentira primero en el PATH que anota sus argumentos y sale 0. Asi se
# prueba la guarda del node_modules enlazado sin tocar ningun paquete real.
#
# Uso:
#   scripts/check.test.sh

set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
check="$repo_root/scripts/check.sh"

tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT

passed=0
failed=0
last_output=""

# El npm de mentira: anota cada llamada en $tmp/npm-calls y sale 0.
mkdir -p "$tmp/bin"
cat > "$tmp/bin/npm" <<EOF
#!/usr/bin/env bash
echo "\$*" >> "$tmp/npm-calls"
exit 0
EOF
chmod +x "$tmp/bin/npm"

# Arma un repo falso limpio en $tmp/repo y borra el registro de llamadas.
fresh_repo() {
  rm -rf "$tmp/repo" "$tmp/other" "$tmp/npm-calls"
  mkdir -p "$tmp/repo/scripts" "$tmp/repo/frontend"
  cp "$check" "$tmp/repo/scripts/check.sh"
}

# Corre check.sh --frontend en el repo falso, afirma el codigo de salida y
# guarda la salida en $last_output.
expect() {
  local name="$1" want="$2"
  local output status
  output="$(PATH="$tmp/bin:$PATH" "$tmp/repo/scripts/check.sh" --frontend 2>&1)"
  status=$?
  # Se guarda ANTES de juzgar el codigo: si no, un expect fallido deja a los
  # expect_output que siguen mirando la salida de una corrida anterior.
  last_output="$output"
  if [ "$status" -ne "$want" ]; then
    echo "FALLO: $name: esperaba salida $want, dio $status" >&2
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
    echo "FALLO: $name: la salida no menciona '$needle'" >&2
    echo "$last_output" | sed 's/^/       /' >&2
    failed=$((failed + 1))
    return 1
  fi
  passed=$((passed + 1))
  echo "ok: $name"
}

# Afirma una condicion de prueba de bash (los argumentos van a `test`).
expect_true() {
  local name="$1"
  shift
  if ! test "$@"; then
    echo "FALLO: $name" >&2
    failed=$((failed + 1))
    return 1
  fi
  passed=$((passed + 1))
  echo "ok: $name"
}

# Afirma que el npm de mentira recibio una llamada exacta.
expect_npm_call() {
  local name="$1" call="$2"
  if ! grep -qxF -- "$call" "$tmp/npm-calls" 2>/dev/null; then
    echo "FALLO: $name: npm no recibio '$call'" >&2
    sed 's/^/       /' "$tmp/npm-calls" >&2 2>/dev/null
    failed=$((failed + 1))
    return 1
  fi
  passed=$((passed + 1))
  echo "ok: $name"
}

# --- 1. node_modules enlazado a otro checkout: se niega sin llamar a npm ----
fresh_repo
mkdir -p "$tmp/other/node_modules/some-package"
echo '{}' > "$tmp/other/node_modules/some-package/package.json"
ln -s "$tmp/other/node_modules" "$tmp/repo/frontend/node_modules"
expect "node_modules enlazado → rojo" 1
expect_output "el fallo nombra el destino del enlace" "$tmp/other/node_modules"
expect_output "el fallo dice como arreglarlo" "rm frontend/node_modules"
expect_true "npm nunca se llamo" ! -e "$tmp/npm-calls"
expect_true "el destino del enlace sigue intacto" -f "$tmp/other/node_modules/some-package/package.json"

# --- 2. node_modules es un directorio de verdad: corre los pasos de npm -----
fresh_repo
mkdir -p "$tmp/repo/frontend/node_modules"
expect "node_modules real → verde" 0
expect_npm_call "npm ci se llamo" "ci"
expect_npm_call "npm run build se llamo" "run build"
expect_npm_call "npm run lint se llamo" "run lint"
expect_npm_call "npm run format:check se llamo" "run format:check"

# --- 3. Sin node_modules: corre npm ci -------------------------------------
fresh_repo
expect "sin node_modules → verde" 0
expect_npm_call "npm ci se llamo sin node_modules" "ci"

echo
echo "check.test: $passed ok, $failed fallos"
[ "$failed" -eq 0 ]
