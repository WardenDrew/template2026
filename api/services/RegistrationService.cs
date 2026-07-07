using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NodaTime;
using TemplateApi.Data;
using TemplateApi.Models;
using TemplateApi.Options;

namespace TemplateApi.Services;

public sealed class RegistrationService(
    AppDbContext dbContext,
    IClock clock,
    IOptions<RegistrationTokenOptions> registrationTokenOptions,
    RegistrationTokenService registrationTokenService,
    OpaqueAuthenticationService opaqueAuthenticationService,
    TotpService totpService
)
{
    private const string ActiveStatus = "active";
    private const string PasswordAuthMethod = "password";
    private const string TotpAuthMethod = "totp";
    private readonly RegistrationTokenOptions registrationTokenOptions =
        registrationTokenOptions.Value;

    public async Task<RegistrationStartResult> StartRegistrationAsync(
        string? email,
        string? displayName,
        CancellationToken ct
    )
    {
        var normalizedEmail = NormalizeEmail(email);

        if (
            await dbContext.Users.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail && user.Status == ActiveStatus,
                ct
            )
        )
        {
            throw new AuthServiceException(
                "A user with this email already exists.",
                StatusCodes.Status409Conflict
            );
        }

        var now = clock.GetCurrentInstant();
        var registrationToken = registrationTokenService.Serialize(
            new RegistrationToken(
                normalizedEmail,
                NormalizeOptional(displayName),
                PasswordConfigured: false,
                TotpConfigured: false,
                OpaqueRegistrationRecordJson: null,
                TotpVerifierJson: null,
                IssuedAt: now,
                ExpiresAt: CalculateRegistrationTokenExpiresAt(now)
            )
        );

        return new RegistrationStartResult(registrationToken, "passwordSetup");
    }

    public Task<RegistrationPasswordStartResult> StartRegistrationPasswordAsync(
        string? registrationToken,
        string? blindedElementBase64,
        CancellationToken ct
    )
    {
        var token = DeserializeRegistrationToken(registrationToken);

        if (token.PasswordConfigured)
        {
            throw new AuthServiceException(
                "Registration password setup is already complete.",
                StatusCodes.Status409Conflict
            );
        }

        var opaqueStart = opaqueAuthenticationService.CreateRegistrationStart(blindedElementBase64);

        return Task.FromResult(
            new RegistrationPasswordStartResult(
                registrationToken!,
                "finishPassword",
                opaqueStart.ServerKeyId,
                opaqueStart.ServerPublicKeyBase64,
                opaqueStart.EvaluatedElementBase64
            )
        );
    }

    public async Task<RegistrationPasswordSetupResult> SetupRegistrationPasswordAsync(
        string? registrationToken,
        string? opaqueRegistrationRecordJson,
        CancellationToken ct
    )
    {
        var token = DeserializeRegistrationToken(registrationToken);

        if (token.PasswordConfigured)
        {
            throw new AuthServiceException(
                "Registration password setup is already complete.",
                StatusCodes.Status409Conflict
            );
        }

        if (string.IsNullOrWhiteSpace(opaqueRegistrationRecordJson))
        {
            throw new AuthServiceException(
                "OPAQUE registration record is required.",
                StatusCodes.Status400BadRequest
            );
        }

        opaqueAuthenticationService.ValidateRegistrationRecord(opaqueRegistrationRecordJson);

        var normalizedEmail = NormalizeEmail(token.Email);

        if (
            await dbContext.Users.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail && user.Status == ActiveStatus,
                ct
            )
        )
        {
            throw new AuthServiceException(
                "A user with this email already exists.",
                StatusCodes.Status409Conflict
            );
        }

        var now = clock.GetCurrentInstant();
        var updatedRegistrationToken = registrationTokenService.Serialize(
            token with
            {
                Email = normalizedEmail,
                PasswordConfigured = true,
                OpaqueRegistrationRecordJson = opaqueRegistrationRecordJson,
                IssuedAt = now,
                ExpiresAt = CalculateRegistrationTokenExpiresAt(now),
            }
        );

        return new RegistrationPasswordSetupResult(updatedRegistrationToken, "totp");
    }

    public Task<RegistrationTotpSetupResult> SetupRegistrationTotpAsync(
        string? registrationToken,
        string? totpSecret,
        string? code,
        CancellationToken ct
    )
    {
        var token = DeserializeRegistrationToken(registrationToken);
        var opaqueRegistrationRecordJson = token.OpaqueRegistrationRecordJson;

        if (!token.PasswordConfigured || string.IsNullOrWhiteSpace(opaqueRegistrationRecordJson))
        {
            throw new AuthServiceException(
                "Registration password setup is incomplete.",
                StatusCodes.Status400BadRequest
            );
        }

        opaqueAuthenticationService.ValidateRegistrationRecord(opaqueRegistrationRecordJson);

        if (string.IsNullOrWhiteSpace(totpSecret))
        {
            throw new AuthServiceException(
                "TOTP secret is required.",
                StatusCodes.Status400BadRequest
            );
        }

        if (!totpService.ValidateCode(totpSecret, code))
        {
            throw new AuthServiceException(
                "TOTP code is invalid.",
                StatusCodes.Status400BadRequest
            );
        }

        var now = clock.GetCurrentInstant();
        var updatedRegistrationToken = registrationTokenService.Serialize(
            token with
            {
                TotpConfigured = true,
                TotpVerifierJson = totpService.CreateVerifierJson(totpSecret),
                IssuedAt = now,
                ExpiresAt = CalculateRegistrationTokenExpiresAt(now),
            }
        );

        return Task.FromResult(
            new RegistrationTotpSetupResult(updatedRegistrationToken, "complete")
        );
    }

    public async Task<RegistrationCompleteResult> CompleteRegistrationAsync(
        string? registrationToken,
        string? displayName,
        CancellationToken ct
    )
    {
        var token = DeserializeRegistrationToken(registrationToken);
        var opaqueRegistrationRecordJson = token.OpaqueRegistrationRecordJson;
        var totpVerifierJson = token.TotpVerifierJson;

        if (
            !token.PasswordConfigured
            || !token.TotpConfigured
            || string.IsNullOrWhiteSpace(opaqueRegistrationRecordJson)
            || string.IsNullOrWhiteSpace(totpVerifierJson)
        )
        {
            throw new AuthServiceException(
                "Registration setup is incomplete.",
                StatusCodes.Status400BadRequest
            );
        }

        opaqueAuthenticationService.ValidateRegistrationRecord(opaqueRegistrationRecordJson);
        ValidateJson(totpVerifierJson, "TOTP authentication method verifier");

        var now = clock.GetCurrentInstant();
        var normalizedEmail = NormalizeEmail(token.Email);

        if (
            await dbContext.Users.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail && user.Status == ActiveStatus,
                ct
            )
        )
        {
            throw new AuthServiceException(
                "A user with this email already exists.",
                StatusCodes.Status409Conflict
            );
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            NormalizedEmail = normalizedEmail,
            DisplayName = NormalizeOptional(displayName) ?? token.DisplayName,
            Status = ActiveStatus,
            CreatedAt = now,
            RegisteredAt = now,
        };

        dbContext.Users.Add(user);
        dbContext.UserAuthMethods.Add(
            CreateAuthMethod(user.Id, PasswordAuthMethod, opaqueRegistrationRecordJson, now)
        );
        dbContext.UserAuthMethods.Add(
            CreateAuthMethod(user.Id, TotpAuthMethod, totpVerifierJson, now)
        );

        await dbContext.SaveChangesAsync(ct);

        return new RegistrationCompleteResult("login", ToSimpleUserDto(user));
    }

    private RegistrationToken DeserializeRegistrationToken(string? registrationToken)
    {
        if (string.IsNullOrWhiteSpace(registrationToken))
        {
            throw new AuthServiceException(
                "Registration token is required.",
                StatusCodes.Status400BadRequest
            );
        }

        return registrationTokenService.Deserialize(registrationToken);
    }

    private UserAuthMethod CreateAuthMethod(
        Guid userId,
        string methodType,
        string verifierJson,
        Instant now
    )
    {
        ValidateJson(verifierJson, "Authentication method verifier");

        return new UserAuthMethod
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MethodType = methodType,
            Status = ActiveStatus,
            VerifierJson = verifierJson,
            CreatedAt = now,
        };
    }

    private Instant CalculateRegistrationTokenExpiresAt(Instant now)
    {
        return now.Plus(Duration.FromMinutes(registrationTokenOptions.ExpirationMinutes));
    }

    private static void ValidateJson(string? json, string label)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new AuthServiceException(
                $"{label} is required.",
                StatusCodes.Status400BadRequest
            );
        }

        try
        {
            using var _ = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            throw new AuthServiceException(
                $"{label} must be valid JSON: {exception.Message}",
                StatusCodes.Status400BadRequest
            );
        }
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

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static SimpleUserDto ToSimpleUserDto(User user)
    {
        return new SimpleUserDto(user.Id, user.Email, user.DisplayName, user.Status);
    }

    public sealed record RegistrationStartResult(string RegistrationToken, string NextStep);

    public sealed record RegistrationPasswordStartResult(
        string RegistrationToken,
        string NextStep,
        string ServerKeyId,
        string ServerPublicKeyBase64,
        string EvaluatedElementBase64
    );

    public sealed record RegistrationPasswordSetupResult(string RegistrationToken, string NextStep);

    public sealed record RegistrationTotpSetupResult(string RegistrationToken, string NextStep);

    public sealed record RegistrationCompleteResult(string NextStep, SimpleUserDto User);
}
