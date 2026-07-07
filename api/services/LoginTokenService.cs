using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using TemplateApi.Models;
using TemplateApi.Options;

namespace TemplateApi.Services;

public sealed class LoginTokenService
{
    private const string TokenTypeClaim = "token_type";
    private const string LoginTokenType = "loginToken";
    private const string PasswordVerifiedClaim = "password_verified";
    private const string TotpVerifiedClaim = "totp_verified";
    private const string OpaqueLoginStateClaim = "opaque_login_state";
    private const string ScopeClaim = "scope";

    private readonly LoginTokenOptions options;
    private readonly JwtSecurityTokenHandler tokenHandler = CreateTokenHandler();
    private readonly TokenKeySet signingKeys;
    private readonly TokenKeySet encryptionKeys;

    public LoginTokenService(IOptions<LoginTokenOptions> options)
    {
        this.options = options.Value;
        signingKeys = TokenKeySet.Create(
            this.options.SigningKeys,
            this.options.ActiveSigningKeyId,
            "Login token signing"
        );
        encryptionKeys = TokenKeySet.Create(
            this.options.EncryptionKeys,
            this.options.ActiveEncryptionKeyId,
            "Login token encryption"
        );
    }

    public string Serialize(LoginToken loginToken)
    {
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Audience = options.Audience,
            Issuer = options.Issuer,
            Subject = new ClaimsIdentity(CreateClaims(loginToken)),
            IssuedAt = loginToken.IssuedAt.ToDateTimeUtc(),
            NotBefore = loginToken.IssuedAt.ToDateTimeUtc(),
            Expires = loginToken.ExpiresAt.ToDateTimeUtc(),
            SigningCredentials = new SigningCredentials(
                signingKeys.ActiveKey,
                SecurityAlgorithms.HmacSha256
            ),
            EncryptingCredentials = new EncryptingCredentials(
                encryptionKeys.ActiveKey,
                SecurityAlgorithms.Aes256KW,
                SecurityAlgorithms.Aes256CbcHmacSha512
            ),
        };

        return tokenHandler.CreateEncodedJwt(tokenDescriptor);
    }

    public LoginToken Deserialize(string serializedToken)
    {
        return ReadTokenPayload(principal => new LoginToken(
            Guid.Parse(GetRequiredClaim(principal, JwtRegisteredClaimNames.Sub)),
            GetRequiredClaim(principal, JwtRegisteredClaimNames.Email),
            ParseScopes(principal),
            GetRequiredBooleanClaim(principal, PasswordVerifiedClaim),
            GetRequiredBooleanClaim(principal, TotpVerifiedClaim),
            principal.FindFirstValue(OpaqueLoginStateClaim),
            GetRequiredInstantClaim(principal, JwtRegisteredClaimNames.Iat),
            GetRequiredInstantClaim(principal, JwtRegisteredClaimNames.Exp)
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

    private static IReadOnlyList<Claim> CreateClaims(LoginToken loginToken)
    {
        var claims = new List<Claim>
        {
            new Claim(TokenTypeClaim, LoginTokenType),
            new Claim(JwtRegisteredClaimNames.Sub, loginToken.UserId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, loginToken.Email),
            new Claim(ScopeClaim, string.Join(' ', loginToken.RequestedScopes)),
            new Claim(
                PasswordVerifiedClaim,
                loginToken.PasswordVerified ? "true" : "false",
                ClaimValueTypes.Boolean
            ),
            new Claim(
                TotpVerifiedClaim,
                loginToken.TotpVerified ? "true" : "false",
                ClaimValueTypes.Boolean
            ),
        };

        if (!string.IsNullOrWhiteSpace(loginToken.OpaqueLoginStateJson))
        {
            claims.Add(new Claim(OpaqueLoginStateClaim, loginToken.OpaqueLoginStateJson));
        }

        return claims;
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

            if (!string.Equals(actualTokenType, LoginTokenType, StringComparison.Ordinal))
            {
                throw new SecurityTokenValidationException(
                    "Token type did not match a login token."
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
            TokenDecryptionKeyResolver = (_, _, keyId, _) => encryptionKeys.Resolve(keyId),
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
            $"Login token is invalid: {exception.Message}",
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

    private static bool GetRequiredBooleanClaim(ClaimsPrincipal principal, string claimType)
    {
        return string.Equals(
            GetRequiredClaim(principal, claimType),
            "true",
            StringComparison.OrdinalIgnoreCase
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
}
