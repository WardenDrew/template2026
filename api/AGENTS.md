# API Instructions

- Prefer the clean current-state API and schema shape. Do not add compatibility
  layers, versioned DTO copies, or migration shims unless explicitly requested.
- Use one class per file by default.
- Organize API code by responsibility:
  - `endpoints/` for FastEndpoints vertical slices, grouped by route segment.
  - `services/` for business workflows, authorization checks, validation, and
    external integrations.
  - `data/` for EF Core entities and the `DbContext`.
  - `models/` for shared API DTOs and value models that are reused across
    endpoint slices.
  - `options/` for externally supplied configuration bound with
    `IOptions<T>`.
  - `jobs/` for background commands and handlers.
- Keep EF entities data-focused. Do not mix business rules into entity classes.
- Give public EF entity properties XML documentation.
- Prefer Data Annotations for entity configuration. Use fluent EF configuration
  only when there is no annotation for the required behavior.
- Do not configure the same entity behavior with both Data Annotations and the
  fluent API.
- Put relationship configuration in a nested
  `IEntityTypeConfiguration<TEntity>` class when fluent configuration is needed.
- Use nullable lifecycle timestamps such as `DeletedAt`, `DisabledAt`, and
  `AcceptedAt` instead of boolean lifecycle flags.
- Bind route parameters through FastEndpoints request DTO properties decorated
  with `[RouteParam]`.
- Keep endpoint classes thin: route/auth configuration, request binding,
  service call, and service-exception translation only.
- Put request DTOs, response DTOs, validators, and other vertical-slice-only
  types inside the endpoint class.
- Store DTOs in `models/` only when they are reused across endpoint slices.
- Catch domain/service exceptions at the endpoint boundary and translate them
  to FastEndpoints validation/problem responses.
- Keep authentication and authorization behind service abstractions. Do not
  parse claims, tokens, headers, or route values throughout business services.
- Use `IClock` for timestamps. Do not call `DateTime.UtcNow` for domain
  timestamps when NodaTime is available.
- Bind configuration through options classes in `options/`, validate required
  settings on startup, and fail fast when required configuration is missing.
- Keep externally visible error responses boring and actionable. Do not leak
  provider internals or stack traces through API responses.
- OPAQUE auth is part of this template. Do not copy project-encryption key
  DTOs, key wrapping, recovery, or encrypted payload mechanisms into this
  template unless the target product explicitly requires them.
- Do not manually edit EF migration files. If a migration is wrong, delete or
  remove it through EF tooling and regenerate it.
- Run `dotnet csharpier format ./<api-project>` after modifying a .NET project.
- Run `dotnet build <api-project>/<api-project>.csproj` after API code changes
  unless the user explicitly asks for documentation-only work.
