# ============================================================================
# Imagen de PostgreSQL para DESARROLLO LOCAL.
#
# Hornea los scripts de inicialización dentro de la imagen en lugar de
# montarlos como bind mount. En Docker Desktop (macOS, VirtioFS) el bit de
# ejecución de los archivos montados se presenta de forma no determinista, lo
# que hace que el entrypoint de postgres a veces intente EJECUTAR init.sh desde
# el mount y falle con "bad interpreter: Permission denied". Al copiarlos a la
# imagen, los permisos son estables y el script corre siempre igual.
#
# Solo se usa en local (docker-compose.override.yml). Stage/producción siguen
# usando la imagen oficial postgres:17-alpine (en Linux el bind mount funciona).
# ============================================================================
FROM postgres:17-alpine

COPY init.sh /docker-entrypoint-initdb.d/

RUN chmod +x /docker-entrypoint-initdb.d/init.sh
