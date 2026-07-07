using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using TemplateApi.Models;
using TemplateApi.Options;

namespace TemplateApi.Services;

public sealed class AccessTokenService
{
    private const string TokenTypeClaim = "token_type";
    private const string AccessTokenType = "accessToken";
    private const string SessionIdClaim = "sid";
    private const string IssueKindClaim = "issue_kind";
    private const string ScopeClaim = "scope";

    private readonly AccessTokenOptions options;
    private readonly JwtSecurityTokenHandler tokenHandler = CreateTokenHandler();
    private readonly TokenKeySet signingKeys;

    public AccessTokenService(IOptions<AccessTokenOptions> options)
    {
        this.options = options.Value;
        signingKeys = TokenKeySet.Create(
            this.options.SigningKeys,
            this.options.ActiveSigningKeyId,
            "Access token signing"
        );
    }

    public string Serialize(AccessToken accessToken)
    {
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Audience = options.Audience,
            Issuer = options.Issuer,
            Subject = new ClaimsIdentity([
                new Claim(TokenTypeClaim, AccessTokenType),
                new Claim(JwtRegisteredClaimNames.Sub, accessToken.UserId.ToString()),
                new Claim(SessionIdClaim, accessToken.SessionId.ToString()),
                new Claim(ScopeClaim, string.Join(' ', accessToken.Scopes)),
                new Claim(IssueKindClaim, FormatIssueKind(accessToken.IssueKind)),
            ]),
            IssuedAt = accessToken.IssuedAt.ToDateTimeUtc(),
            NotBefore = accessToken.IssuedAt.ToDateTimeUtc(),
            Expires = accessToken.ExpiresAt.ToDateTimeUtc(),
            SigningCredentials = new SigningCredentials(
                signingKeys.ActiveKey,
                SecurityAlgorithms.HmacSha256
            ),
        };

        return tokenHandler.CreateEncodedJwt(tokenDescriptor);
    }

    public AccessToken Deserialize(string serializedToken)
    {
        return ReadTokenPayload(principal => new AccessToken(
            Guid.Parse(GetRequiredClaim(principal, JwtRegisteredClaimNames.Sub)),
            Guid.Parse(GetRequiredClaim(principal, SessionIdClaim)),
            ParseScopes(principal),
            GetRequiredInstantClaim(principal, JwtRegisteredClaimNames.Iat),
            GetRequiredInstantClaim(principal, JwtRegisteredClaimNames.Exp),
            ParseIssueKind(GetRequiredClaim(principal, IssueKindClaim))
        ));

        TPayload ReadTokenPayload<TPayload>(Func<ClaimsPrincipal, TPayload> readPayload)
        {
            try
            {
                return readPayload(ValidateToken(serializedToken));
            }
            catch (SecurityTokenException exception)
            {
                throw CreateInvalidTokenException(exception);
            }
            catch (ArgumentException exception)
            {
                throw CreateInvalidTokenException(exception);
            }
            catch (FormatException exception)
            {
                throw CreateInvalidTokenException(exception);
            }
            catch (OverflowException exception)
            {
                throw CreateInvalidTokenException(exception);
            }
        }
    }

    private ClaimsPrincipal ValidateToken(string serializedToken)
    {
        try
        {
            var principal = tokenHandler.ValidateToken(
                serializedToken,
                CreateValidationParameters(),
                out _
            );
            var actualTokenType = GetRequiredClaim(principal, TokenTypeClaim);

            if (!string.Equals(actualTokenType, AccessTokenType, StringComparison.Ordinal))
            {
                throw new SecurityTokenValidationException(
                    "Token type did not match an access token."
                );
            }

            return principal;
        }
        catch (SecurityTokenException exception)
        {
            throw CreateInvalidTokenException(exception);
        }
        catch (ArgumentException exception)
        {
            throw CreateInvalidTokenException(exception);
        }
    }

    private TokenValidationParameters CreateValidationParameters()
    {
        return new TokenValidationParameters
        {
            ClockSkew = TimeSpan.FromSeconds(30),
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidAudience = options.Audience,
            ValidIssuer = options.Issuer,
            IssuerSigningKeyResolver = (_, _, keyId, _) => signingKeys.Resolve(keyId),
        };
    }

    private static JwtSecurityTokenHandler CreateTokenHandler()
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        handler.OutboundClaimTypeMap.Clear();

        return handler;
    }

    private static AuthServiceException CreateInvalidTokenException(Exception exception)
    {
        return new AuthServiceException(
            $"Access token is invalid: {exception.Message}",
            StatusCodes.Status401Unauthorized
        );
    }

    private static string GetRequiredClaim(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirstValue(claimType)
            ?? throw new SecurityTokenValidationException(
                $"Required claim '{claimType}' is missing."
            );
    }

    private static Instant GetRequiredInstantClaim(ClaimsPrincipal principal, string claimType)
    {
        var value = GetRequiredClaim(principal, claimType);

        if (
            !long.TryParse(
                value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var seconds
            )
        )
        {
            throw new SecurityTokenValidationException(
                $"Required claim '{claimType}' is not a valid unix timestamp."
            );
        }

        return Instant.FromUnixTimeSeconds(seconds);
    }

    private static IReadOnlyList<string> ParseScopes(ClaimsPrincipal principal)
    {
        var scopeValue = principal.FindFirstValue(ScopeClaim);

        if (string.IsNullOrWhiteSpace(scopeValue))
        {
            return [];
        }

        return scopeValue
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string FormatIssueKind(AccessTokenIssueKind issueKind)
    {
        return issueKind switch
        {
            AccessTokenIssueKind.Interactive => "interactive",
            AccessTokenIssueKind.Refresh => "refresh",
            _ => throw new ArgumentOutOfRangeException(nameof(issueKind)),
        };
    }

    private static AccessTokenIssueKind ParseIssueKind(string value)
    {
        return value switch
        {
            "interactive" => AccessTokenIssueKind.Interactive,
            "refresh" => AccessTokenIssueKind.Refresh,
            _ => throw new SecurityTokenValidationException("Access token issue kind is invalid."),
        };
    }
}
