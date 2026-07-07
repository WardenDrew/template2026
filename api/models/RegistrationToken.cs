using NodaTime;

namespace TemplateApi.Models;

public sealed record RegistrationToken(
    string Email,
    string? DisplayName,
    bool PasswordConfigured,
    bool TotpConfigured,
    string? OpaqueRegistrationRecordJson,
    string? TotpVerifierJson,
    Instant IssuedAt,
    Instant ExpiresAt
);
