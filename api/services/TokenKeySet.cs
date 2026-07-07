using System.Globalization;
using Microsoft.IdentityModel.Tokens;
using TemplateApi.Options;

namespace TemplateApi.Services;

internal sealed class TokenKeySet
{
    private const int SymmetricKeyByteLength = 32;
    private readonly IReadOnlyDictionary<string, SymmetricSecurityKey> keys;

    private TokenKeySet(string activeKeyId, IReadOnlyDictionary<string, SymmetricSecurityKey> keys)
    {
        ActiveKeyId = activeKeyId;
        this.keys = keys;
    }

    public string ActiveKeyId { get; }

    public SymmetricSecurityKey ActiveKey => keys[ActiveKeyId];

    public static TokenKeySet Create(
        IReadOnlyList<TokenKeyOptions> configuredKeys,
        string activeKeyId,
        string purpose
    )
    {
        if (string.IsNullOrWhiteSpace(activeKeyId))
        {
            throw new InvalidOperationException($"{purpose} active key id is required.");
        }

        if (configuredKeys.Count == 0)
        {
            throw new InvalidOperationException($"{purpose} must configure at least one key.");
        }

        var keys = CreateConfiguredKeys(configuredKeys, purpose);

        if (!keys.ContainsKey(activeKeyId))
        {
            throw new InvalidOperationException(
                $"{purpose} active key id '{activeKeyId}' does not match a configured key."
            );
        }

        return new TokenKeySet(activeKeyId, keys);
    }

    public IEnumerable<SecurityKey> Resolve(string? keyId)
    {
        if (!string.IsNullOrWhiteSpace(keyId) && keys.TryGetValue(keyId, out var key))
        {
            return [key];
        }

        return [];
    }

    private static IReadOnlyDictionary<string, SymmetricSecurityKey> CreateConfiguredKeys(
        IReadOnlyList<TokenKeyOptions> configuredKeys,
        string purpose
    )
    {
        var keys = new Dictionary<string, SymmetricSecurityKey>(StringComparer.Ordinal);

        foreach (var configuredKey in configuredKeys)
        {
            if (string.IsNullOrWhiteSpace(configuredKey.KeyId))
            {
                throw new InvalidOperationException($"{purpose} key id is required.");
            }

            if (keys.ContainsKey(configuredKey.KeyId))
            {
                throw new InvalidOperationException(
                    $"{purpose} key id '{configuredKey.KeyId}' is duplicated."
                );
            }

            if (string.IsNullOrWhiteSpace(configuredKey.KeyBase64))
            {
                throw new InvalidOperationException(
                    $"{purpose} key '{configuredKey.KeyId}' base64 value is required."
                );
            }

            byte[] keyBytes;

            try
            {
                keyBytes = Convert.FromBase64String(configuredKey.KeyBase64);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException(
                    $"{purpose} key '{configuredKey.KeyId}' base64 value is invalid.",
                    exception
                );
            }

            if (keyBytes.Length < SymmetricKeyByteLength)
            {
                throw new InvalidOperationException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{purpose} key '{configuredKey.KeyId}' must decode to at least {SymmetricKeyByteLength} bytes."
                    )
                );
            }

            keys[configuredKey.KeyId] = new SymmetricSecurityKey(keyBytes)
            {
                KeyId = configuredKey.KeyId,
            };
        }

        return keys;
    }
}
