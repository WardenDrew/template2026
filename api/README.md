# API Template

This template captures the reusable API shape:

- ASP.NET Core on current .NET.
- FastEndpoints for endpoint slices.
- Entity Framework Core with Npgsql/Postgres and NodaTime.
- OPAQUE password registration/login.
- TOTP enrollment and verification.
- JWT access/login/registration tokens.
- Constructor-injected services.
- Validated options classes.
- Startup-time database creation for the local container stack.
- FastEndpoints job queues backed by EF records.

## Folder Layout

```text
api/
  api.csproj
  Program.cs
  appsettings.json
  endpoints/
    auth/
    health/
    resources/
    user/
  data/
    AppDbContext.cs
    User.cs
    UserAuthMethod.cs
    UserSession.cs
    Resource.cs
    QueuedJob.cs
  jobs/
  models/
  options/
  services/
  migrations/
```

## Composition Root

Keep `Program.cs` responsible for wiring infrastructure only:

- Load checked-in defaults, then optional local overrides from
  `conf/appsettings.local.json`.
- Register FastEndpoints, authorization, the clock, the DbContext pool, options,
  domain services, external clients, and job queues.
- Validate required options on startup.
- Ensure the local development database exists at startup.
- Add middleware once, then call `UseFastEndpoints()` with problem details.

See `Program.cs`, `api.csproj`, and `appsettings.json`.

## Endpoint Slices

Endpoint classes should stay thin:

- Route and auth/scope configuration in `Configure()`.
- Route parameters in nested request DTOs using `[RouteParam]`.
- Slice-only request/response/validator types nested inside the endpoint.
- Calls into a service for business behavior.
- Domain/service exceptions translated at the boundary.

Store DTOs in `models/` only when multiple endpoints share them.

See `endpoints/resources/ListResourcesEndpoint.cs` and
`endpoints/resources/CreateResourceEndpoint.cs`.

See `endpoints/health/HealthzEndpoint.cs` for the public health-check shape.

## Services

Services own business behavior:

- Require authorization through a single `AuthorizationService` abstraction.
- Normalize and validate input close to the command handler.
- Use `IClock` for timestamps.
- Query with EF Core and project to shared DTOs.
- Throw a single service exception type carrying an HTTP status for endpoint
  translation.

See `services/ResourceService.cs`, `services/AuthenticationService.cs`, and
`services/RegistrationService.cs`.

Authentication is intentionally generic: OPAQUE proves the password without
sending it to the server, TOTP supplies a second factor, and bearer access
tokens are validated by `AuthenticationService`.

## EF Entities

Entities are persistence models:

- Use Data Annotations for table names, required fields, max lengths, and
  column types.
- Keep navigation/relationship rules in nested configuration classes only when
  annotations are insufficient.
- Use `Instant` for timestamps and nullable lifecycle timestamps for state.
- Avoid business methods on entities.

See `data/Resource.cs` and `data/AppDbContext.cs`.

## Options

Each options class should define:

- `public const string SectionName`.
- Immutable `init` properties.
- Sensible local defaults only when they are safe.
- Startup validation in `Program.cs`.

See `options/AuthenticationOptions.cs`, `options/AccessTokenOptions.cs`, and
`options/JobQueueOptions.cs`.

## Background Jobs

Use FastEndpoints command/job types when the work can happen out of band:

- `ICommand` for payloads.
- `ICommandHandler<TCommand>` for execution.
- EF-backed queue storage when jobs must survive process restarts.

Keep job payloads simple and serializable. Put provider-specific delivery logic
in services.

See `jobs/NotificationJob.cs`.

## Commands

```sh
dotnet build <api-project>/<api-project>.csproj
dotnet csharpier format ./<api-project>
dotnet ef migrations add <MigrationName> --project <api-project>
```
