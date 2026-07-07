using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using NodaTime;

namespace TemplateApi.Services;

public sealed class TotpService(IClock clock)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string CreateVerifierJson(string secret)
    {
        return JsonSerializer.Serialize(
            new TotpVerifier("TOTP-SHA1", Digits: 6, PeriodSeconds: 30, NormalizeSecret(secret)),
            JsonOptions
        );
    }

    public bool ValidateCode(string secret, string? code)
    {
        return ValidateCode(secret, code, clock.GetCurrentInstant());
    }

    public bool ValidateVerifierJson(string? verifierJson, string? code)
    {
        if (string.IsNullOrWhiteSpace(verifierJson))
        {
            return false;
        }

        TotpVerifier verifier;

        try
        {
            verifier =
                JsonSerializer.Deserialize<TotpVerifier>(verifierJson, JsonOptions)
                ?? throw new AuthServiceException(
                    "TOTP verifier is invalid.",
                    StatusCodes.Status500InternalServerError
                );
        }
        catch (JsonException exception)
        {
            throw new AuthServiceException(
                $"TOTP verifier is invalid: {exception.Message}",
                StatusCodes.Status500InternalServerError
            );
        }

        if (
            !string.Equals(verifier.Algorithm, "TOTP-SHA1", StringComparison.Ordinal)
            || verifier.Digits != 6
            || verifier.PeriodSeconds != 30
        )
        {
            throw new AuthServiceException(
                "TOTP verifier is unsupported.",
                StatusCodes.Status500InternalServerError
            );
        }

        return ValidateCode(verifier.Secret, code);
    }

    private static bool ValidateCode(string secret, string? code, Instant now)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        var normalizedCode = code.Trim();

        if (normalizedCode.Length != 6 || !normalizedCode.All(char.IsAsciiDigit))
        {
            return false;
        }

        var secretBytes = DecodeBase32(NormalizeSecret(secret));
        var unixSeconds = now.ToUnixTimeSeconds();
        var timeStep = unixSeconds / 30;

        for (var offset = -1; offset <= 1; offset++)
        {
            if (
                string.Equals(
                    ComputeCode(secretBytes, timeStep + offset),
                    normalizedCode,
                    StringComparison.Ordinal
                )
            )
            {
                return true;
            }
        }

        return false;
    }

    private static string ComputeCode(byte[] secretBytes, long timeStep)
    {
        Span<byte> counter = stackalloc byte[8];
        BitConverter.TryWriteBytes(counter, timeStep);

        if (BitConverter.IsLittleEndian)
        {
            counter.Reverse();
        }

        using var hmac = new HMACSHA1(secretBytes);
        var hash = hmac.ComputeHash(counter.ToArray());
        var offset = hash[^1] & 0x0f;
        var binaryCode =
            ((hash[offset] & 0x7f) << 24)
            | ((hash[offset + 1] & 0xff) << 16)
            | ((hash[offset + 2] & 0xff) << 8)
            | (hash[offset + 3] & 0xff);
        var code = binaryCode % 1_000_000;

        return code.ToString("000000", CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bits = 0;
        var bitCount = 0;
        var bytes = new List<byte>();

        foreach (var character in value)
        {
            var index = alphabet.IndexOf(character);

            if (index < 0)
            {
                throw new AuthServiceException(
                    "TOTP secret is invalid.",
                    StatusCodes.Status400BadRequest
                );
            }

            bits = (bits << 5) | index;
            bitCount += 5;

            if (bitCount >= 8)
            {
                bytes.Add((byte)(bits >> (bitCount - 8)));
                bitCount -= 8;
                bits &= (1 << bitCount) - 1;
            }
        }

        return bytes.ToArray();
    }

    private static string NormalizeSecret(string secret)
    {
        return secret
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant()
            .TrimEnd('=');
    }

    private sealed record TotpVerifier(
        string Algorithm,
        int Digits,
        int PeriodSeconds,
        string Secret
    );
}
