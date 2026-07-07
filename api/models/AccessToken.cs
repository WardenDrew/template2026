using NodaTime;

namespace TemplateApi.Models;

public sealed record AccessToken(
    Guid UserId,
    Guid SessionId,
    IReadOnlyList<string> Scopes,
    Instant IssuedAt,
    Instant ExpiresAt,
    AccessTokenIssueKind IssueKind
);
