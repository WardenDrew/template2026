using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Template.Opaque;

internal static class OpaqueEncoding
{
    public static byte[] FromBase64(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new OpaqueProtocolException($"{label} is required.");
        }

        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException exception)
        {
            throw new OpaqueProtocolException($"{label} must be base64: {exception.Message}");
        }
    }

    public static string ToBase64(byte[] value)
    {
        return Convert.ToBase64String(value);
    }

    public static byte[] Concat(params byte[][] values)
    {
        var length = 0;

        foreach (var value in values)
        {
            length += value.Length;
        }

        var result = new byte[length];
        var offset = 0;

        foreach (var value in values)
        {
            value.CopyTo(result.AsSpan(offset));
            offset += value.Length;
        }

        return result;
    }

    public static byte[] ConcatLengthPrefixed(params byte[][] values)
    {
        var length = values.Length * sizeof(int);

        foreach (var value in values)
        {
            length += value.Length;
        }

        var result = new byte[length];
        var offset = 0;

        foreach (var value in values)
        {
            BinaryPrimitives.WriteInt32BigEndian(result.AsSpan(offset, sizeof(int)), value.Length);
            offset += sizeof(int);
            value.CopyTo(result.AsSpan(offset));
            offset += value.Length;
        }

        return result;
    }

    public static bool FixedTimeEqualsBase64(string left, string right)
    {
        var leftBytes = FromBase64(left, "Left value");
        var rightBytes = FromBase64(right, "Right value");

        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
