# Prod image: linux/arm64 (Dokploy on Oracle ARM, plan section 10). Local development uses Dockerfile-dev.
# Build context = storeasy-app/. Every VITE_* value is baked in at build time; CI passes them as --build-arg.
# The checkout has no .env, so every value is an arg.
FROM --platform=linux/arm64 node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .

# Empty: the app calls its own origin and nginx below proxies /api, /hubs and /mcp to the API.
ARG VITE_API_URL=
ENV VITE_API_URL=$VITE_API_URL
# AGPL-3.0 section 13: the "Source code" link. Empty means the upstream repository.
ARG VITE_SOURCE_URL=
ENV VITE_SOURCE_URL=$VITE_SOURCE_URL
ARG VITE_APP_VERSION=
ENV VITE_APP_VERSION=$VITE_APP_VERSION
RUN npm run build

FROM --platform=linux/arm64 nginx:alpine AS final
# The API container the proxy talks to; nginx's entrypoint substitutes it into the template at start.
ENV STOREASY_API_UPSTREAM=http://storeasy-api:8080
ENV NGINX_ENVSUBST_FILTER=STOREASY_
COPY nginx.conf /etc/nginx/templates/default.conf.template
COPY nginx-security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
