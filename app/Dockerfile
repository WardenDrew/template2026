FROM node:24-alpine AS build
WORKDIR /workspace

COPY app/ app/
COPY opaque-ts/ opaque-ts/

WORKDIR /workspace/app
RUN corepack enable \
    && yarn install --immutable \
    && yarn build

FROM caddy:2-alpine AS runtime
WORKDIR /srv

COPY app/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /workspace/app/dist/spa/ /srv/

EXPOSE 80
