using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodaTime;
using TemplateApi.Models;
using TemplateApi.Options;

namespace TemplateApi.Services;

public sealed class RegistrationTokenService
{
    private const string TokenTypeClaim = "token_type";
    private const string RegistrationTokenType = "registrationToken";
    private const string DisplayNameClaim = "display_name";
    private const string PasswordConfiguredClaim = "password_configured";
    private const string TotpConfiguredClaim = "totp_configured";
    private const string OpaqueRegistrationRecordClaim = "opaque_registration_record";
    private const string TotpVerifierClaim = "totp_verifier";

    private readonly RegistrationTokenOptions options;
    private readonly JwtSecurityTokenHandler tokenHandler = CreateTokenHandler();
    private readonly TokenKeySet signingKeys;
    private readonly TokenKeySet encryptionKeys;

    public RegistrationTokenService(IOptions<RegistrationTokenOptions> options)
    {
        this.options = options.Value;
        signingKeys = TokenKeySet.Create(
            this.options.SigningKeys,
            this.options.ActiveSigningKeyId,
            "Registration token signing"
        );
        encryptionKeys = TokenKeySet.Create(
            this.options.EncryptionKeys,
            this.options.ActiveEncryptionKeyId,
            "Registration token encryption"
        );
    }

    public string Serialize(RegistrationToken registrationToken)
    {
        var claims = new List<Claim>
        {
            new(TokenTypeClaim, RegistrationTokenType),
            new(JwtRegisteredClaimNames.Email, registrationToken.Email),
            new(
                PasswordConfiguredClaim,
                registrationToken.PasswordConfigured ? "true" : "false",
                ClaimValueTypes.Boolean
            ),
            new(
                TotpConfiguredClaim,
                registrationToken.TotpConfigured ? "true" : "false",
                ClaimValueTypes.Boolean
            ),
        };

        if (!string.IsNullOrWhiteSpace(registrationToken.DisplayName))
        {
            claims.Add(new Claim(DisplayNameClaim, registrationToken.DisplayName));
        }

        if (!string.IsNullOrWhiteSpace(registrationToken.OpaqueRegistrationRecordJson))
        {
            claims.Add(
                new Claim(
                    OpaqueRegistrationRecordClaim,
                    registrationToken.OpaqueRegistrationRecordJson
                )
            );
        }

        if (!string.IsNullOrWhiteSpace(registrationToken.TotpVerifierJson))
        {
            claims.Add(new Claim(TotpVerifierClaim, registrationToken.TotpVerifierJson));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Audience = options.Audience,
            Issuer = options.Issuer,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = registrationToken.IssuedAt.ToDateTimeUtc(),
            NotBefore = registrationToken.IssuedAt.ToDateTimeUtc(),
            Expires = registrationToken.ExpiresAt.ToDateTimeUtc(),
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

    public RegistrationToken Deserialize(string serializedToken)
    {
        return ReadTokenPayload(principal => new RegistrationToken(
            GetRequiredClaim(principal, JwtRegisteredClaimNames.Email),
            GetOptionalClaim(principal, DisplayNameClaim),
            GetRequiredBooleanClaim(principal, PasswordConfiguredClaim),
            GetRequiredBooleanClaim(principal, TotpConfiguredClaim),
            GetOptionalClaim(principal, OpaqueRegistrationRecordClaim),
            GetOptionalClaim(principal, TotpVerifierClaim),
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

            if (!string.Equals(actualTokenType, RegistrationTokenType, StringComparison.Ordinal))
            {
                throw new SecurityTokenValidationException(
                    "Token type did not match a registration token."
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
            $"Registration token is invalid: {exception.Message}",
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

    private static string? GetOptionalClaim(ClaimsPrincipal principal, string claimType)
    {
        return principal.FindFirstValue(claimType);
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
}
