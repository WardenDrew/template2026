using System.Security.Cryptography;

namespace Template.Opaque;

internal static class OpaqueCrypto
{
    public static byte[] HkdfSha256(byte[] inputKeyMaterial, byte[] salt, byte[] info, int length)
    {
        var actualSalt = salt.Length == 0 ? new byte[32] : salt;

        using var extractHmac = new HMACSHA256(actualSalt);
        var pseudorandomKey = extractHmac.ComputeHash(inputKeyMaterial);
        var result = new byte[length];
        var previous = Array.Empty<byte>();
        var offset = 0;
        byte counter = 1;

        while (offset < length)
        {
            using var expandHmac = new HMACSHA256(pseudorandomKey);
            var blockInput = OpaqueEncoding.Concat(previous, info, [counter]);
            previous = expandHmac.ComputeHash(blockInput);
            var bytesToCopy = Math.Min(previous.Length, length - offset);
            previous.AsSpan(0, bytesToCopy).CopyTo(result.AsSpan(offset));
            offset += bytesToCopy;
            counter++;
        }

        return result;
    }

    public static byte[] HmacSha256(byte[] key, byte[] value)
    {
        using var hmac = new HMACSHA256(key);

        return hmac.ComputeHash(value);
    }

    public static byte[] Sha256(byte[] value)
    {
        return SHA256.HashData(value);
    }

    public static byte[] RandomBytes(int length)
    {
        return RandomNumberGenerator.GetBytes(length);
    }
}
