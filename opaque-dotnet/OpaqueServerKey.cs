using System.Numerics;

namespace Template.Opaque;

public sealed class OpaqueServerKey
{
    private OpaqueServerKey(string keyId, BigInteger privateScalar)
    {
        KeyId = RequireKeyId(keyId);
        PrivateScalar = privateScalar;
        var publicPoint = P256.ScalarMultiply(privateScalar, P256.Generator);
        PublicKeyBase64 = P256.SerializePointBase64(publicPoint);
    }

    public string KeyId { get; }

    public string PrivateKeyBase64 => P256.ToScalarBase64(PrivateScalar);

    public string PublicKeyBase64 { get; }

    internal BigInteger PrivateScalar { get; }

    public static OpaqueServerKey Generate(string keyId)
    {
        return new OpaqueServerKey(keyId, P256.RandomScalar());
    }

    public static OpaqueServerKey FromPrivateKeyBase64(string keyId, string privateKeyBase64)
    {
        return new OpaqueServerKey(
            keyId,
            P256.ParseScalarBase64(privateKeyBase64, $"OPAQUE server key '{keyId}'")
        );
    }

    private static string RequireKeyId(string keyId)
    {
        if (string.IsNullOrWhiteSpace(keyId))
        {
            throw new OpaqueProtocolException("OPAQUE server key id is required.");
        }

        return keyId.Trim();
    }
}
