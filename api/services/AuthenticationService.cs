using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;
using TemplateApi.Data;
using TemplateApi.Models;
using TemplateApi.Options;

namespace TemplateApi.Services;

public sealed class AuthenticationService(
    AppDbContext dbContext,
    IClock clock,
    IOptions<LoginTokenOptions> loginTokenOptions,
    IOptions<SystemPolicyOptions> systemPolicyOptions,
    LoginTokenService loginTokenService,
    AccessTokenService accessTokenService,
    OpaqueAuthenticationService opaqueAuthenticationService,
    TotpService totpService
)
{
    private const string ActiveStatus = "active";
    private const string PasswordAuthMethod = "password";
    private const string TotpAuthMethod = "totp";
    private const string AuthenticatedUserContextItemKey = "template.authenticatedUserContext";
    private readonly LoginTokenOptions loginTokenOptions = loginTokenOptions.Value;
    private readonly SystemPolicyOptions systemPolicyOptions = systemPolicyOptions.Value;

    public async Task<LoginStartResult> StartLoginAsync(
        string? email,
        IEnumerable<string>? requestedScopes,
        CancellationToken ct
    )
    {
        var normalizedEmail = NormalizeEmail(email);
        var user = await GetActiveUserByEmailAsync(normalizedEmail, ct);
        _ = await GetActivePasswordAuthMethodAsync(user.Id, ct);
        var scopes = PrepareRequestedScopes(requestedScopes);
        var now = clock.GetCurrentInstant();
        var loginToken = loginTokenService.Serialize(
            new LoginToken(
                user.Id,
                normalizedEmail,
                scopes,
                PasswordVerified: false,
                TotpVerified: false,
                OpaqueLoginStateJson: null,
                IssuedAt: now,
                ExpiresAt: CalculateLoginTokenExpiresAt(now)
            )
        );

        return new LoginStartResult(loginToken, "password");
    }

    public async Task<LoginOpaqueStartResult> StartLoginOpaqueAsync(
        string? loginToken,
        string? blindedElementBase64,
        CancellationToken ct
    )
    {
        var token = DeserializeLoginToken(loginToken);
        var user = await EnsureActiveLoginUserAsync(token, ct);
        var authMethod = await GetActivePasswordAuthMethodAsync(user.Id, ct);

        if (string.IsNullOrWhiteSpace(authMethod.VerifierJson))
        {
            throw new AuthServiceException(
                "Password authentication is not configured.",
                StatusCodes.Status409Conflict
            );
        }

        var opaqueStart = opaqueAuthenticationService.CreateLoginStart(
            blindedElementBase64,
            authMethod.VerifierJson
        );
        var now = clock.GetCurrentInstant();
        var updatedLoginToken = loginTokenService.Serialize(
            token with
            {
                PasswordVerified = false,
                OpaqueLoginStateJson = opaqueAuthenticationService.SerializeLoginState(
                    opaqueStart.State
                ),
                IssuedAt = now,
                ExpiresAt = CalculateLoginTokenExpiresAt(now),
            }
        );

        return new LoginOpaqueStartResult(
            updatedLoginToken,
            "finishPassword",
            opaqueStart.ServerKeyId,
            opaqueStart.EvaluatedElementBase64,
            opaqueStart.ServerPublicKeyBase64,
            opaqueStart.ClientPublicKeyBase64,
            opaqueStart.EnvelopeNonceBase64,
            opaqueStart.EnvelopeCiphertextBase64,
            opaqueStart.ServerNonceBase64,
            opaqueStart.ServerEphemeralPublicKeyBase64
        );
    }

    public async Task<LoginOpaqueFinishResult> FinishLoginOpaqueAsync(
        string? loginToken,
        string? clientNonceBase64,
        string? clientEphemeralPublicKeyBase64,
        string? clientMacBase64,
        CancellationToken ct
    )
    {
        var token = DeserializeLoginToken(loginToken);

        if (string.IsNullOrWhiteSpace(token.OpaqueLoginStateJson))
        {
            throw new AuthServiceException(
                "OPAQUE login state is required.",
                StatusCodes.Status400BadRequest
            );
        }

        var user = await EnsureActiveLoginUserAsync(token, ct);
        var authMethod = await GetActivePasswordAuthMethodAsync(user.Id, ct);

        if (string.IsNullOrWhiteSpace(authMethod.VerifierJson))
        {
            throw new AuthServiceException(
                "Password authentication is not configured.",
                StatusCodes.Status409Conflict
            );
        }

        var opaqueState = opaqueAuthenticationService.DeserializeLoginState(
            token.OpaqueLoginStateJson
        );

        if (
            !opaqueAuthenticationService.VerifyLoginFinish(
                authMethod.VerifierJson,
                opaqueState,
                clientNonceBase64,
                clientEphemeralPublicKeyBase64,
                clientMacBase64
            )
        )
        {
            throw new AuthServiceException(
                "Password authentication failed.",
                StatusCodes.Status401Unauthorized
            );
        }

        var now = clock.GetCurrentInstant();
        authMethod.LastUsedAt = now;

        var updatedLoginToken = loginTokenService.Serialize(
            token with
            {
                PasswordVerified = true,
                TotpVerified = false,
                OpaqueLoginStateJson = null,
                IssuedAt = now,
                ExpiresAt = CalculateLoginTokenExpiresAt(now),
            }
        );

        await dbContext.SaveChangesAsync(ct);

        return new LoginOpaqueFinishResult(
            updatedLoginToken,
            await HasActiveTotpAuthMethodAsync(user.Id, ct) ? "verifyTotp" : "complete"
        );
    }

    public async Task<LoginTotpVerifyResult> VerifyLoginTotpAsync(
        string? loginToken,
        string? code,
        CancellationToken ct
    )
    {
        var token = DeserializeLoginToken(loginToken);

        if (!token.PasswordVerified)
        {
            throw new AuthServiceException(
                "Password authentication is incomplete.",
                StatusCodes.Status400BadRequest
            );
        }

        var user = await EnsureActiveLoginUserAsync(token, ct);
        var authMethod = await GetActiveTotpAuthMethodAsync(user.Id, ct);

        if (!totpService.ValidateVerifierJson(authMethod.VerifierJson, code))
        {
            throw new AuthServiceException(
                "TOTP code is invalid.",
                StatusCodes.Status400BadRequest
            );
        }

        var now = clock.GetCurrentInstant();
        authMethod.LastUsedAt = now;

        var updatedLoginToken = loginTokenService.Serialize(
            token with
            {
                TotpVerified = true,
                IssuedAt = now,
                ExpiresAt = CalculateLoginTokenExpiresAt(now),
            }
        );

        await dbContext.SaveChangesAsync(ct);

        return new LoginTotpVerifyResult(updatedLoginToken, "complete");
    }

    public async Task<LoginCompleteResult> CompleteLoginAsync(
        string? loginToken,
        CancellationToken ct
    )
    {
        var token = DeserializeLoginToken(loginToken);

        if (
            !token.PasswordVerified
            || (await HasActiveTotpAuthMethodAsync(token.UserId, ct) && !token.TotpVerified)
        )
        {
            throw new AuthServiceException(
                "Login authentication is incomplete.",
                StatusCodes.Status400BadRequest
            );
        }

        var user = await EnsureActiveLoginUserAsync(token, ct);
        var now = clock.GetCurrentInstant();
        var issuedAccessToken = IssueInteractiveAccessTokenForUser(
            user,
            now,
            token.RequestedScopes
        );

        await dbContext.SaveChangesAsync(ct);

        return new LoginCompleteResult(
            issuedAccessToken.AccessToken,
            issuedAccessToken.ExpiresAt,
            issuedAccessToken.Scopes,
            ToSimpleUserDto(user)
        );
    }

    public async Task<RefreshAccessTokenResult> RefreshAccessTokenAsync(
        HttpContext httpContext,
        IEnumerable<string>? requestedScopes,
        CancellationToken ct
    )
    {
        var context = await GetAuthenticatedUserAndSessionAsync(httpContext, ct);
        var now = clock.GetCurrentInstant();

        context.Session.LastUsedAt = now;
        context.Session.ExpiresAt = CalculateSessionExpiresAt(now);

        var scopes = PrepareRequestedScopes(requestedScopes ?? context.AccessToken.Scopes);
        var issuedAccessToken = IssueAccessToken(
            context.User,
            context.Session,
            now,
            AccessTokenIssueKind.Refresh,
            scopes
        );

        await dbContext.SaveChangesAsync(ct);

        return new RefreshAccessTokenResult(
            issuedAccessToken.AccessToken,
            issuedAccessToken.ExpiresAt,
            issuedAccessToken.Scopes,
            ToSimpleUserDto(context.User)
        );
    }

    public async Task<SimpleUserDto> GetCurrentUserAsync(
        HttpContext httpContext,
        CancellationToken ct
    )
    {
        var context = await AuthenticateAccessTokenAsync(httpContext, ct);

        return ToSimpleUserDto(context.User);
    }

    public async Task LogoutAsync(HttpContext httpContext, CancellationToken ct)
    {
        var context = await GetAuthenticatedUserAndSessionAsync(httpContext, ct);

        context.Session.RevokedAt = clock.GetCurrentInstant();
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<AuthenticatedUserContext> AuthenticateAccessTokenAsync(
        HttpContext httpContext,
        CancellationToken ct
    )
    {
        var context = await GetAuthenticatedUserAndSessionAsync(httpContext, ct);

        httpContext.User = CreateClaimsPrincipal(context);
        context.Session.LastUsedAt = clock.GetCurrentInstant();
        await dbContext.SaveChangesAsync(ct);

        return context;
    }

    private IssuedAccessToken IssueInteractiveAccessTokenForUser(
        User user,
        Instant now,
        IReadOnlyList<string> scopes
    )
    {
        var session = CreateSession(user.Id, now);
        dbContext.UserSessions.Add(session);

        return IssueAccessToken(user, session, now, AccessTokenIssueKind.Interactive, scopes);
    }

    private async Task<AuthenticatedUserContext> GetAuthenticatedUserAndSessionAsync(
        HttpContext httpContext,
        CancellationToken ct
    )
    {
        if (
            httpContext.Items.TryGetValue(AuthenticatedUserContextItemKey, out var cachedContext)
            && cachedContext is AuthenticatedUserContext authenticatedUserContext
        )
        {
            return authenticatedUserContext;
        }

        var accessToken = accessTokenService.Deserialize(ExtractBearerToken(httpContext));
        var now = clock.GetCurrentInstant();
        var session = await dbContext.UserSessions.SingleOrDefaultAsync(
            session => session.Id == accessToken.SessionId,
            ct
        );

        if (
            session is null
            || session.UserId != accessToken.UserId
            || session.RevokedAt is not null
            || session.ExpiresAt <= now
            || accessToken.ExpiresAt > session.ExpiresAt
        )
        {
            throw new AuthServiceException(
                "Session is invalid or expired.",
                StatusCodes.Status401Unauthorized
            );
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(
            user => user.Id == accessToken.UserId && user.Status == ActiveStatus,
            ct
        );

        if (user is null)
        {
            throw new AuthServiceException(
                "User is invalid or disabled.",
                StatusCodes.Status401Unauthorized
            );
        }

        var context = new AuthenticatedUserContext(user, session, accessToken);
        httpContext.Items[AuthenticatedUserContextItemKey] = context;

        return context;
    }

    private async Task<User> EnsureActiveLoginUserAsync(LoginToken token, CancellationToken ct)
    {
        var normalizedEmail = NormalizeEmail(token.Email);
        var user = await dbContext.Users.SingleOrDefaultAsync(
            user =>
                user.Id == token.UserId
                && user.NormalizedEmail == normalizedEmail
                && user.Status == ActiveStatus,
            ct
        );

        return user
            ?? throw new AuthServiceException(
                "User was not found.",
                StatusCodes.Status401Unauthorized
            );
    }

    private async Task<User> GetActiveUserByEmailAsync(string normalizedEmail, CancellationToken ct)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(
            user => user.NormalizedEmail == normalizedEmail && user.Status == ActiveStatus,
            ct
        );

        return user
            ?? throw new AuthServiceException("User was not found.", StatusCodes.Status404NotFound);
    }

    private async Task<UserAuthMethod> GetActivePasswordAuthMethodAsync(
        Guid userId,
        CancellationToken ct
    )
    {
        var authMethod = await dbContext.UserAuthMethods.FirstOrDefaultAsync(
            method =>
                method.UserId == userId
                && method.MethodType == PasswordAuthMethod
                && method.Status == ActiveStatus,
            ct
        );

        return authMethod
            ?? throw new AuthServiceException(
                "Password authentication is not configured.",
                StatusCodes.Status404NotFound
            );
    }

    private async Task<bool> HasActiveTotpAuthMethodAsync(Guid userId, CancellationToken ct)
    {
        return await dbContext.UserAuthMethods.AnyAsync(
            method =>
                method.UserId == userId
                && method.MethodType == TotpAuthMethod
                && method.Status == ActiveStatus,
            ct
        );
    }

    private async Task<UserAuthMethod> GetActiveTotpAuthMethodAsync(
        Guid userId,
        CancellationToken ct
    )
    {
        var authMethod = await dbContext.UserAuthMethods.FirstOrDefaultAsync(
            method =>
                method.UserId == userId
                && method.MethodType == TotpAuthMethod
                && method.Status == ActiveStatus,
            ct
        );

        return authMethod
            ?? throw new AuthServiceException(
                "TOTP authentication is not configured.",
                StatusCodes.Status404NotFound
            );
    }

    private UserSession CreateSession(Guid userId, Instant now)
    {
        return new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = CalculateSessionExpiresAt(now),
        };
    }

    private LoginToken DeserializeLoginToken(string? loginToken)
    {
        if (string.IsNullOrWhiteSpace(loginToken))
        {
            throw new AuthServiceException(
                "Login token is required.",
                StatusCodes.Status400BadRequest
            );
        }

        return loginTokenService.Deserialize(loginToken);
    }

    private Instant CalculateLoginTokenExpiresAt(Instant now)
    {
        return now.Plus(Duration.FromMinutes(loginTokenOptions.ExpirationMinutes));
    }

    private Instant CalculateSessionExpiresAt(Instant now)
    {
        return now.Plus(Duration.FromMinutes(systemPolicyOptions.SessionExpirationMinutes));
    }

    private IssuedAccessToken IssueAccessToken(
        User user,
        UserSession session,
        Instant now,
        AccessTokenIssueKind issueKind,
        IReadOnlyList<string> scopes
    )
    {
        var accessToken = new AccessToken(
            user.Id,
            session.Id,
            scopes,
            now,
            session.ExpiresAt,
            issueKind
        );

        return new IssuedAccessToken(
            accessTokenService.Serialize(accessToken),
            FormatInstant(accessToken.ExpiresAt),
            accessToken.Scopes
        );
    }

    private static IReadOnlyList<string> PrepareRequestedScopes(
        IEnumerable<string>? requestedScopes
    )
    {
        var scopes =
            requestedScopes
                ?.Select(scope => scope.Trim())
                .Where(scope => scope.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            ?? [];

        return scopes.Length == 0 ? [".default"] : scopes;
    }

    private static string ExtractBearerToken(HttpContext httpContext)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();

        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new AuthServiceException(
                "Bearer access token is required.",
                StatusCodes.Status401Unauthorized
            );
        }

        return authorization["Bearer ".Length..].Trim();
    }

    private static string NormalizeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new AuthServiceException("Email is required.", StatusCodes.Status400BadRequest);
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (!normalizedEmail.Contains('@', StringComparison.Ordinal))
        {
            throw new AuthServiceException("Email is invalid.", StatusCodes.Status400BadRequest);
        }

        return normalizedEmail;
    }

    private static string FormatInstant(Instant instant)
    {
        return instant.ToString();
    }

    private static ClaimsPrincipal CreateClaimsPrincipal(AuthenticatedUserContext context)
    {
        return new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, context.User.Id.ToString()),
                    new Claim("scope", string.Join(' ', context.AccessToken.Scopes)),
                ],
                "template-access-token"
            )
        );
    }

    private static SimpleUserDto ToSimpleUserDto(User user)
    {
        return new SimpleUserDto(user.Id, user.Email, user.DisplayName, user.Status);
    }

    public sealed record AuthenticatedUserContext(
        User User,
        UserSession Session,
        AccessToken AccessToken
    );

    public sealed record IssuedAccessToken(
        string AccessToken,
        string ExpiresAt,
        IReadOnlyList<string> Scopes
    );

    public sealed record LoginStartResult(string LoginToken, string NextStep);

    public sealed record LoginOpaqueStartResult(
        string LoginToken,
        string NextStep,
        string ServerKeyId,
        string EvaluatedElementBase64,
        string ServerPublicKeyBase64,
        string ClientPublicKeyBase64,
        string EnvelopeNonceBase64,
        string EnvelopeCiphertextBase64,
        string ServerNonceBase64,
        string ServerEphemeralPublicKeyBase64
    );

    public sealed record LoginOpaqueFinishResult(string LoginToken, string NextStep);

    public sealed record LoginTotpVerifyResult(string LoginToken, string NextStep);

    public sealed record LoginCompleteResult(
        string AccessToken,
        string ExpiresAt,
        IReadOnlyList<string> Scopes,
        SimpleUserDto User
    );

    public sealed record RefreshAccessTokenResult(
        string AccessToken,
        string ExpiresAt,
        IReadOnlyList<string> Scopes,
        SimpleUserDto User
    );
}
