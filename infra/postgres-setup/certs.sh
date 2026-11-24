#!/bin/sh
# ===========================================================================
# La CA y el certificado del servidor de PostgreSQL.
#
# Lo corre el contenedor `postgres-certs` en CADA despliegue, antes de que arranque
# postgres. Es idempotente:
#   - la primera vez crea una CA propia de este entorno (diez años) y un certificado
#     para DNS:postgres (dos años) firmado por ella;
#   - después solo renueva el certificado del servidor cuando le quedan menos de
#     RENEW_BEFORE_DAYS días (45), o cuando ya no verifica contra la CA. La CA no se
#     reemplaza nunca: cada cliente confía en ella, y una CA nueva obliga a
#     redesplegarlos a todos.
#
# Sale con error si al terminar el certificado vence en menos de 45 días: es la alarma
# del trabajo mensual que la corre fuera de los despliegues (docs/deploy-sorteo360.md §7).
# 45 y no 30 para que ese trabajo tenga casi siempre dos intentos antes del vencimiento,
# y quien atiende la alarma, más de un mes para arreglarlo.
#
# Dos volúmenes, para que ningún servicio vea una llave:
#   /tls  postgres-tls: la CA con su llave, el certificado y la llave del servidor.
#         Solo lo montan este contenedor y, de lectura, postgres.
#   /ca   postgres-ca: solo ca.crt. Lo montan, de lectura, todos los clientes.
#
# La llave del servidor es del usuario de postgres (uid 70 en la imagen Alpine) con
# modo 600: si no, postgres se niega a arrancar. Con otra imagen de postgres, fijar
# POSTGRES_UID al uid de su usuario (`id postgres` adentro de ella).
#
# Un certificado renovado con postgres ya corriendo lo carga el SELECT pg_reload_conf()
# con que termina roles.sh, que corre después en el mismo despliegue.
# ===========================================================================
set -eu

tls_dir="${TLS_DIR:-/tls}"
ca_dir="${CA_DIR:-/ca}"
server_name="${SERVER_NAME:-postgres}"
postgres_uid="${POSTGRES_UID:-70}"
renew_before_days="${RENEW_BEFORE_DAYS:-45}"
server_cert_days="${SERVER_CERT_DAYS:-730}"
# Igual a la ventana por defecto, para que una renovación que no ocurrió salte en el primer
# intento. Fijo y aparte de RENEW_BEFORE_DAYS: forzar una renovación con una ventana grande
# no tiene que disparar la alarma.
min_valid_days=45

umask 077
# Los archivos se arman en una carpeta del mismo volumen y se mueven a su lugar, así
# que un corte a mitad de camino nunca deja un archivo escrito a medias. Un par que
# quedó desparejo lo detecta y lo rehace la próxima corrida.
work="$tls_dir/.work"
rm -rf "$work"
mkdir "$work"
trap 'rm -rf "$work"' EXIT

if [ ! -s "$tls_dir/ca.key" ] || [ ! -s "$tls_dir/ca.crt" ]; then
  echo "postgres-certs: creando la CA del entorno"
  openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out "$work/ca.key"
  openssl req -x509 -new -key "$work/ca.key" -sha256 -days 3650 \
    -subj "/CN=Sorteo360 PostgreSQL CA" \
    -addext "basicConstraints=critical,CA:TRUE" \
    -addext "keyUsage=critical,keyCertSign,cRLSign" \
    -out "$work/ca.crt"
  mv "$work/ca.key" "$tls_dir/ca.key"
  mv "$work/ca.crt" "$tls_dir/ca.crt"
  # Una CA nueva invalida cualquier certificado viejo que hubiera quedado.
  rm -f "$tls_dir/server.crt"
fi
chown 0:0 "$tls_dir/ca.key"
chmod 600 "$tls_dir/ca.key"
chmod 644 "$tls_dir/ca.crt"

# Verdadero cuando falta el certificado del servidor, vence dentro de la ventana, ya no
# verifica contra la CA para el nombre del servidor, o la llave no es la suya (un corte
# entre los dos mv de abajo deja la llave nueva junto al certificado viejo).
needs_server_cert() {
  [ -s "$tls_dir/server.crt" ] && [ -s "$tls_dir/server.key" ] || return 0
  [ "$(openssl pkey -in "$tls_dir/server.key" -pubout 2>/dev/null)" = \
    "$(openssl x509 -in "$tls_dir/server.crt" -pubkey -noout 2>/dev/null)" ] || return 0
  openssl x509 -checkend $((renew_before_days * 86400)) -noout -in "$tls_dir/server.crt" >/dev/null || return 0
  openssl verify -CAfile "$tls_dir/ca.crt" -verify_hostname "$server_name" "$tls_dir/server.crt" >/dev/null 2>&1 || return 0
  return 1
}

if needs_server_cert; then
  echo "postgres-certs: emitiendo el certificado de $server_name"
  openssl genpkey -algorithm EC -pkeyopt ec_paramgen_curve:P-256 -out "$work/server.key"
  openssl req -new -key "$work/server.key" -subj "/CN=$server_name" -out "$work/server.csr"
  cat > "$work/server.ext" <<EOF
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature
extendedKeyUsage=serverAuth
subjectAltName=DNS:$server_name
EOF
  openssl x509 -req -in "$work/server.csr" -CA "$tls_dir/ca.crt" -CAkey "$tls_dir/ca.key" \
    -CAcreateserial -CAserial "$work/ca.srl" -days "$server_cert_days" -sha256 \
    -extfile "$work/server.ext" -out "$work/server.crt"
  chown "$postgres_uid:$postgres_uid" "$work/server.key" "$work/server.crt"
  chmod 600 "$work/server.key"
  chmod 644 "$work/server.crt"
  # Postgres lee el par solo al arrancar o al recargar, nunca entre estos dos mv.
  mv "$work/server.key" "$tls_dir/server.key"
  mv "$work/server.crt" "$tls_dir/server.crt"
fi
chown "$postgres_uid:$postgres_uid" "$tls_dir/server.key" "$tls_dir/server.crt"
chmod 600 "$tls_dir/server.key"
chmod 644 "$tls_dir/server.crt"

# Lo único que ven los clientes. Se copia en cada corrida: un volumen de clientes
# recreado vuelve a tener la CA sin tocar nada más.
cp "$tls_dir/ca.crt" "$ca_dir/.ca.crt.new"
chmod 644 "$ca_dir/.ca.crt.new"
mv "$ca_dir/.ca.crt.new" "$ca_dir/ca.crt"

expires="$(openssl x509 -enddate -noout -in "$tls_dir/server.crt" | cut -d= -f2)"
if ! openssl x509 -checkend $((min_valid_days * 86400)) -noout -in "$tls_dir/server.crt" >/dev/null; then
  echo "postgres-certs: ERROR: el certificado de $server_name vence en menos de $min_valid_days días ($expires) y no se pudo renovar" >&2
  echo "postgres-certs: qué hacer: docs/deploy-sorteo360.md §7, \"Si la renovación del certificado de postgres falla\"" >&2
  exit 1
fi
echo "postgres-certs: listo; el certificado de $server_name vence el $expires"
