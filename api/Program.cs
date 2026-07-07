using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;
using TemplateApi.Data;
using TemplateApi.Options;
using TemplateApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile(
    "conf/appsettings.local.json",
    optional: true,
    reloadOnChange: true
);

builder.Services.AddFastEndpoints();
builder.Services.AddJobQueues<QueuedJob, QueuedJobStorageProvider>();
builder.Services.AddSingleton<IClock>(SystemClock.Instance);

void ConfigureDbContext(DbContextOptionsBuilder options)
{
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Connection string 'DefaultConnection' is required."
        );

    options.UseNpgsql(connectionString, npgsql => npgsql.UseNodaTime());
}

builder.Services.AddDbContextPool<AppDbContext>(ConfigureDbContext);
builder.Services.AddPooledDbContextFactory<AppDbContext>(ConfigureDbContext);

builder
    .Services.AddOptions<AccessTokenOptions>()
    .Bind(builder.Configuration.GetRequiredSection(AccessTokenOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Issuer),
        "Access token issuer is required."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Audience),
        "Access token audience is required."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ActiveSigningKeyId),
        "Access token active signing key id is required."
    )
    .Validate(
        options => options.SigningKeys is { Count: > 0 },
        "At least one access token signing key must be configured."
    )
    .Validate(
        options => HasKey(options.SigningKeys, options.ActiveSigningKeyId),
        "Access token signing keys must include the active signing key."
    )
    .ValidateOnStart();

builder
    .Services.AddOptions<RegistrationTokenOptions>()
    .Bind(builder.Configuration.GetRequiredSection(RegistrationTokenOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Issuer),
        "Registration token issuer is required."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Audience),
        "Registration token audience is required."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ActiveSigningKeyId),
        "Registration token active signing key id is required."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ActiveEncryptionKeyId),
        "Registration token active encryption key id is required."
    )
    .Validate(
        options => options.SigningKeys is { Count: > 0 },
        "At least one registration token signing key must be configured."
    )
    .Validate(
        options => options.EncryptionKeys is { Count: > 0 },
        "At least one registration token encryption key must be configured."
    )
    .Validate(
        options =>
            HasKey(options.SigningKeys, options.ActiveSigningKeyId)
            && HasKey(options.EncryptionKeys, options.ActiveEncryptionKeyId),
        "Registration token keys must include the active key ids."
    )
    .Validate(
        options => options.ExpirationMinutes > 0,
        "Registration token expiration must be greater than zero."
    )
    .ValidateOnStart();

builder
    .Services.AddOptions<LoginTokenOptions>()
    .Bind(builder.Configuration.GetRequiredSection(LoginTokenOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Issuer),
        "Login token issuer is required."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.Audience),
        "Login token audience is required."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ActiveSigningKeyId),
        "Login token active signing key id is required."
    )
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ActiveEncryptionKeyId),
        "Login token active encryption key id is required."
    )
    .Validate(
        options => options.SigningKeys is { Count: > 0 },
        "At least one login token signing key must be configured."
    )
    .Validate(
        options => options.EncryptionKeys is { Count: > 0 },
        "At least one login token encryption key must be configured."
    )
    .Validate(
        options =>
            HasKey(options.SigningKeys, options.ActiveSigningKeyId)
            && HasKey(options.EncryptionKeys, options.ActiveEncryptionKeyId),
        "Login token keys must include the active key ids."
    )
    .Validate(
        options => options.ExpirationMinutes > 0,
        "Login token expiration must be greater than zero."
    )
    .ValidateOnStart();

builder
    .Services.AddOptions<AuthenticationOptions>()
    .Bind(builder.Configuration.GetRequiredSection(AuthenticationOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.OpaqueActiveKeyId),
        "OPAQUE active key id is required."
    )
    .Validate(
        options => options.OpaqueServerKeys is { Count: > 0 },
        "At least one OPAQUE server key must be configured."
    )
    .Validate(
        options => options.OpaqueServerKeys.Any(key => key.KeyId == options.OpaqueActiveKeyId),
        "OPAQUE server keys must include the active key id."
    )
    .ValidateOnStart();

builder
    .Services.AddOptions<SystemPolicyOptions>()
    .Bind(builder.Configuration.GetRequiredSection(SystemPolicyOptions.SectionName))
    .Validate(
        options => options.SessionExpirationMinutes > 0,
        "Session expiration must be greater than zero."
    )
    .ValidateOnStart();

builder
    .Services.AddOptions<TemplateApi.Options.JobQueueOptions>()
    .Bind(builder.Configuration.GetRequiredSection(TemplateApi.Options.JobQueueOptions.SectionName))
    .Validate(
        options => options.MaxConcurrency > 0,
        "Job queue max concurrency must be greater than zero."
    )
    .Validate(
        options => options.StorageProbeDelaySeconds > 0,
        "Job queue storage probe delay must be greater than zero."
    )
    .Validate(
        options => options.RetryDelaySeconds > 0,
        "Job queue retry delay must be greater than zero."
    )
    .Validate(
        options => options.ExecutionTimeLimitSeconds > 0,
        "Job queue execution time limit must be greater than zero."
    )
    .Validate(
        options => options.FailureRetryDelaySeconds > 0,
        "Job queue failure retry delay must be greater than zero."
    )
    .Validate(
        options => options.LeaseDurationSeconds > 0,
        "Job queue lease duration must be greater than zero."
    );

builder.Services.AddSingleton<AccessTokenService>();
builder.Services.AddSingleton<LoginTokenService>();
builder.Services.AddSingleton<RegistrationTokenService>();
builder.Services.AddSingleton<OpaqueAuthenticationService>();
builder.Services.AddScoped<AuthenticationService>();
builder.Services.AddScoped<AuthorizationService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<ResourceService>();
builder.Services.AddScoped<TotpService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.UseFastEndpoints(config =>
{
    config.Errors.UseProblemDetails();
});

app.UseJobQueues(options =>
{
    var jobQueueOptions = app
        .Services.GetRequiredService<IOptions<TemplateApi.Options.JobQueueOptions>>()
        .Value;

    options.MaxConcurrency = jobQueueOptions.MaxConcurrency;
    options.StorageProbeDelay = TimeSpan.FromSeconds(jobQueueOptions.StorageProbeDelaySeconds);
    options.RetryDelay = TimeSpan.FromSeconds(jobQueueOptions.RetryDelaySeconds);
    options.ExecutionTimeLimit = TimeSpan.FromSeconds(jobQueueOptions.ExecutionTimeLimitSeconds);
    options.Warmup();
});

app.Run();

static bool HasKey(IReadOnlyList<TokenKeyOptions> keys, string activeKeyId)
{
    return keys.Any(key =>
        string.Equals(key.KeyId, activeKeyId, StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(key.KeyBase64)
    );
}
