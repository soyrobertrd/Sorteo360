# syntax=docker/dockerfile:1

# ============================================================================
# Etapa de build: compila la SPA con Vite.
# Vite inyecta las variables VITE_* en el bundle EN BUILD, no en runtime:
# cambiar el modo de datos o la URL del API requiere reconstruir la imagen.
# ============================================================================
FROM node:22-alpine AS build
WORKDIR /src

# Copiar primero los manifiestos para aprovechar la caché de capas en el install.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# http = consume eventos-service real (nginx reenvía /v1 dentro de la red).
# VITE_EVENTOS_API_URL vacía = la app llama /v1 en su mismo origen.
# VITE_KEYCLOAK_URL = Keycloak visto desde el navegador (login OIDC).
ARG VITE_API_MODE=http
ARG VITE_EVENTOS_API_URL=
ARG VITE_KEYCLOAK_URL=http://localhost:8082
# false = el landing no ofrece crear cuenta (el registro del servicio todavía no existe, A25).
ARG VITE_SIGNUP_ENABLED=false
ARG VITE_CONTACT_EMAIL=
ENV VITE_API_MODE=$VITE_API_MODE \
    VITE_EVENTOS_API_URL=$VITE_EVENTOS_API_URL \
    VITE_KEYCLOAK_URL=$VITE_KEYCLOAK_URL \
    VITE_SIGNUP_ENABLED=$VITE_SIGNUP_ENABLED \
    VITE_CONTACT_EMAIL=$VITE_CONTACT_EMAIL
RUN npm run build

# ============================================================================
# Etapa de runtime: nginx sin privilegios (usuario no-root, puerto 8080) que
# sirve los estáticos y reenvía /v1 a eventos-service por DNS de la red.
# ============================================================================
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /src/dist /usr/share/nginx/html

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -qO- http://127.0.0.1:8080/_alive || exit 1
