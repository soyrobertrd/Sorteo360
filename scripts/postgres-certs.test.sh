#!/usr/bin/env bash
#
# Tests de infra/postgres-setup/certs.sh: la CA y el certificado del
# servidor de PostgreSQL. Corre el script en la misma imagen que usa el contenedor
# `postgres-certs`, sobre dos volúmenes de Docker temporales, y afirma:
#
#   - la primera corrida crea la CA y un certificado para DNS:postgres firmado por ella;
#   - la llave del servidor es del usuario de postgres (uid 70) con modo 600, o
#     postgres no arranca;
#   - el volumen de los clientes solo recibe ca.crt, nunca una llave;
#   - una segunda corrida no cambia nada;
#   - con la ventana de renovación forzada, renueva el certificado y NO la CA.
#
# Necesita Docker; no corre en scripts/check.sh ni en CI.
#
# Uso:
#   scripts/postgres-certs.test.sh

set -uo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
setup_dir="$repo_root/infra/postgres-setup"
image="sorteo360/postgres-certs:test"
suffix="$$"
tls_volume="sorteo360-certs-test-tls-$suffix"
ca_volume="sorteo360-certs-test-ca-$suffix"

cleanup() {
  docker volume rm -f "$tls_volume" "$ca_volume" >/dev/null 2>&1
  docker image rm "$image" >/dev/null 2>&1
}
trap cleanup EXIT

docker build -q -t "$image" "$setup_dir" >/dev/null || {
  echo "postgres-certs.test: no se pudo construir la imagen" >&2
  exit 2
}

passed=0
failed=0

check() {
  local name="$1"
  shift
  if "$@"; then
    passed=$((passed + 1))
  else
    echo "FALLO: $name" >&2
    failed=$((failed + 1))
  fi
}

# Corre certs.sh como lo corre el compose: los dos volúmenes y el script montado.
run_certs() {
  docker run --rm "$@" \
    -v "$tls_volume:/tls" -v "$ca_volume:/ca" \
    -v "$setup_dir/certs.sh:/setup/certs.sh:ro" \
    "$image" sh /setup/certs.sh
}

# Corre un comando con los dos volúmenes montados, para inspeccionarlos.
inspect() {
  docker run --rm -v "$tls_volume:/tls" -v "$ca_volume:/ca" "$image" sh -c "$1"
}

fingerprint() {
  inspect "openssl x509 -noout -fingerprint -sha256 -in $1"
}

echo "== primera corrida"
check "la primera corrida sale con 0" run_certs >/dev/null
check "el certificado verifica contra la CA para el nombre postgres" \
  inspect "openssl verify -CAfile /tls/ca.crt -verify_hostname postgres /tls/server.crt >/dev/null"
check "el certificado NO verifica para otro nombre" \
  inspect "openssl verify -CAfile /tls/ca.crt -verify_hostname localhost /tls/server.crt 2>&1 | grep -q 'hostname mismatch'"
check "la llave del servidor es de uid 70 con modo 600" \
  test "$(inspect 'stat -c %u:%a /tls/server.key')" = "70:600"
check "la llave de la CA es de root con modo 600" \
  test "$(inspect 'stat -c %u:%a /tls/ca.key')" = "0:600"
check "el volumen de los clientes tiene solo ca.crt" \
  test "$(inspect 'ls /ca')" = "ca.crt"
check "el ca.crt de los clientes es el de la CA" \
  inspect "cmp -s /ca/ca.crt /tls/ca.crt"
check "la CA vale unos diez años" \
  inspect "openssl x509 -checkend $((3640 * 86400)) -noout -in /tls/ca.crt >/dev/null"
check "el certificado del servidor vence antes de dos años y un día" \
  inspect "openssl x509 -checkend $((731 * 86400)) -noout -in /tls/server.crt | grep -q 'will expire'"

ca_before="$(fingerprint /tls/ca.crt)"
server_before="$(fingerprint /tls/server.crt)"

echo "== segunda corrida: no cambia nada"
check "la segunda corrida sale con 0" run_certs >/dev/null
check "la CA no cambia" test "$(fingerprint /tls/ca.crt)" = "$ca_before"
check "el certificado del servidor no cambia" test "$(fingerprint /tls/server.crt)" = "$server_before"

echo "== renovación forzada"
check "la corrida forzada sale con 0" run_certs -e RENEW_BEFORE_DAYS=1000 >/dev/null
check "la CA no cambia al renovar" test "$(fingerprint /tls/ca.crt)" = "$ca_before"
check "el certificado del servidor es otro" test "$(fingerprint /tls/server.crt)" != "$server_before"
check "el certificado renovado verifica contra la misma CA" \
  inspect "openssl verify -CAfile /ca/ca.crt -verify_hostname postgres /tls/server.crt >/dev/null"
check "la llave renovada sigue siendo de uid 70 con modo 600" \
  test "$(inspect 'stat -c %u:%a /tls/server.key')" = "70:600"

echo "== llave que no corresponde al certificado (un corte entre los dos mv)"
server_before="$(fingerprint /tls/server.crt)"
inspect "openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out /tls/server.key 2>/dev/null && chown 70:70 /tls/server.key && chmod 600 /tls/server.key"
check "la corrida sale con 0" run_certs >/dev/null
check "emite un certificado nuevo" test "$(fingerprint /tls/server.crt)" != "$server_before"
check "la llave y el certificado vuelven a ser un par" \
  inspect "[ \"\$(openssl pkey -in /tls/server.key -pubout)\" = \"\$(openssl x509 -in /tls/server.crt -pubkey -noout)\" ]"

echo "== alarma: el certificado sigue por vencer después de correr"
# SERVER_CERT_DAYS corto simula una renovación que no sirvió: el certificado nuevo
# también vence dentro de la ventana, y la corrida tiene que fallar para que el trabajo
# programado que la llama quede en rojo.
check "sale con error si el certificado queda con menos de 45 días" \
  sh -c "! docker run --rm -e SERVER_CERT_DAYS=10 -e RENEW_BEFORE_DAYS=1000 -v $tls_volume:/tls -v $ca_volume:/ca \
    -v $setup_dir/certs.sh:/setup/certs.sh:ro $image sh /setup/certs.sh >/dev/null 2>&1"
check "el error dice cuándo vence" \
  sh -c "docker run --rm -e SERVER_CERT_DAYS=10 -e RENEW_BEFORE_DAYS=0 -v $tls_volume:/tls -v $ca_volume:/ca \
    -v $setup_dir/certs.sh:/setup/certs.sh:ro $image sh /setup/certs.sh 2>&1 | grep -q 'vence en menos de 45 días'"
check "el error apunta al runbook" \
  sh -c "docker run --rm -e SERVER_CERT_DAYS=10 -e RENEW_BEFORE_DAYS=0 -v $tls_volume:/tls -v $ca_volume:/ca \
    -v $setup_dir/certs.sh:/setup/certs.sh:ro $image sh /setup/certs.sh 2>&1 | grep -q 'docs/deploy-sorteo360.md'"
check "una corrida normal lo renueva y vuelve a salir con 0" run_certs >/dev/null

echo "== ventana de 45 días"
# 40 días: dentro de la ventana de 45, fuera de una de 30.
run_certs -e SERVER_CERT_DAYS=40 -e RENEW_BEFORE_DAYS=1000 >/dev/null 2>&1
server_before="$(fingerprint /tls/server.crt)"
check "con 40 días por delante, una corrida normal lo renueva" run_certs >/dev/null
check "y el certificado es otro" test "$(fingerprint /tls/server.crt)" != "$server_before"

echo "postgres-certs.test: $passed ok, $failed fallo(s)"
[ "$failed" -eq 0 ]
