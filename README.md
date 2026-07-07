# Project Template

This is a runnable skeleton project extracted from The Grid's reusable API,
GUI, and Docker Compose shape without carrying forward the project-encryption,
key-wrapping, recovery, organization/project, or encrypted payload model.

Use it as a cloneable starting point:

```sh
docker compose up --build
```

Useful local URLs:

- Web app: `http://localhost:5000`
- API health through Caddy: `http://localhost:5000/api/healthz`

## Project Areas

- `AGENTS.md` - root guidance that should be copied into new projects using
  this template.
- `api/` - ASP.NET Core API conventions built around FastEndpoints, EF Core,
  Npgsql/Postgres, NodaTime, options validation, OPAQUE login/signup, TOTP,
  services, and vertical endpoint slices.
- `app/` - Quasar/Vue app with login, registration, TOTP code entry, route
  metadata, layout-owned navigation, typed API clients, and resource examples.
- `opaque-dotnet/` - server-side OPAQUE implementation used by the API.
- `opaque-ts/` - browser-side OPAQUE implementation used by the SPA.
- `conf/` - ignored local override mount for `appsettings.local.json`.
- `docker-compose.yml`, `api.Dockerfile`, `web.Dockerfile`, `Caddyfile` -
  local web/API/Postgres stack with a same-origin `/api` reverse proxy.

## What Is Intentionally Excluded

- Client-side key generation, key wrapping, encrypted payload formats, and
  encrypted-record DTOs.
- Recovery phrase, operator recovery, organization/project crypto principal,
  and encrypted workspace flows.
- Project-specific branding.
- Existing EF migrations.

The API uses `EnsureCreatedAsync()` so a fresh clone can boot immediately.
Generate real EF migrations once the target product schema settles.

## Development Commands

API build:

```sh
dotnet build api/api.csproj
```

Frontend checks:

```sh
cd app
corepack enable
yarn install --immutable
yarn lint:check
yarn typecheck
yarn build
```

Compose validation:

```sh
docker compose config
```

## Carry Forward Notes

- Copy the relevant `AGENTS.md` files into generated projects so future AI work
  inherits the same boundaries and verification expectations.
- Prefer the clean current-state API/schema/UI shape over compatibility layers
  while the product is still moving quickly.
- Keep business logic in services, not EF entities or endpoint handlers.
- Keep endpoint request/response DTOs nested in the endpoint unless they are
  shared by multiple endpoints.
- Keep frontend state explicit: loading, error, empty, success, and busy states
  should be visible in the page or reusable workflow component that owns them.
- Run the project-specific formatter and build/typecheck commands before
  finishing changes.
# template2026
