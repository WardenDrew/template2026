using NodaTime;

namespace TemplateApi.Models;

public sealed record LoginToken(
    Guid UserId,
    string Email,
    IReadOnlyList<string> RequestedScopes,
    bool PasswordVerified,
    bool TotpVerified,
    string? OpaqueLoginStateJson,
    Instant IssuedAt,
    Instant ExpiresAt
);
