# ============================================================================
# Imagen del contenedor `postgres-certs`: Alpine con el binario openssl,
# que postgres:17-alpine no trae. Solo eso: el script, certs.sh, se monta desde el
# repo igual que roles.sh, así que cambiarlo no obliga a reconstruir nada.
# ============================================================================
FROM alpine:3.22

RUN apk add --no-cache openssl
