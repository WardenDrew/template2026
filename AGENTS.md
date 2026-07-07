# Template Instructions

These templates are meant to preserve reusable project principles, not this
repository's product-specific implementation.

## General

- Prefer the clean current-state API/schema/UI shape over backwards
  compatibility shims while the product is still under active design.
- Keep template code generic. OPAQUE is intentionally included for password
  auth, but do not copy project-encryption key-wrapping flows, encrypted
  payload DTOs, recovery mechanisms, or product branding into a new project
  unless the new product explicitly needs them.
- Replace placeholder names consistently before building: namespaces, project
  names, package names, storage keys, database names, Docker image paths, ports,
  route names, and displayed product text.
- Keep generated projects small at first. Add infrastructure only when the
  product uses it, but keep the same boundaries: API endpoints call services,
  services own business rules, entities stay data-focused, and GUI pages compose
  reusable components.
- Store local-only secrets and machine-specific overrides outside committed
  defaults. Commit examples, not real credentials.
- Do not edit generated dependency folders such as `node_modules/`, build
  outputs, `.quasar/`, `dist/`, `bin/`, or `obj/`.

## Formatting And Verification

- For modified .NET projects, run `dotnet csharpier format ./<api-project>`
  before ending.
- For modified Quasar projects, run `yarn lint:check`, `yarn typecheck`, and
  `yarn build` from the app folder.
- For Docker/local stack changes, run `docker compose config` from the project
  root.
- If a command cannot be run, report that clearly with the reason.

## Database And Migrations

- Never manually edit EF migration files. If a migration is wrong, remove it
  with EF tooling or delete/regenerate migrations only when explicitly
  requested, then generate a new migration with `dotnet ef migrations add`.
- Model normal relationships normally. Do not avoid foreign keys or navigation
  properties merely because records may later contain sensitive payloads.
- Use nullable lifecycle timestamps such as `DeletedAt`, `DisabledAt`,
  `RevokedAt`, and `AcceptedAt` instead of boolean lifecycle flags.

## AI Working Notes

- Read the nearest `AGENTS.md` before changing files in a copied template.
- Preserve user changes. Do not revert unrelated edits or regenerate broad
  areas unless the user asks.
- Keep changes scoped to the requested template area.
- Prefer established local patterns over introducing new framework layers.
