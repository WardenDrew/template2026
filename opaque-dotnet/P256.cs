using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace Template.Opaque;

internal static class P256
{
    private static readonly BigInteger Prime = FromHex(
        "FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF"
    );
    private static readonly BigInteger Order = FromHex(
        "FFFFFFFF00000000FFFFFFFFFFFFFFFFBCE6FAADA7179E84F3B9CAC2FC632551"
    );
    private static readonly BigInteger CurveB = FromHex(
        "5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B"
    );
    private static readonly BigInteger GeneratorX = FromHex(
        "6B17D1F2E12C4247F8BCE6E563A440F277037D812DEB33A0F4A13945D898C296"
    );
    private static readonly BigInteger GeneratorY = FromHex(
        "4FE342E2FE1A7F9B8EE7EB4A7C0F9E162BCE33576B315ECECBB6406837BF51F5"
    );

    private const int FieldByteLength = 32;
    private const byte UncompressedPointPrefix = 0x04;

    public static P256Point Generator { get; } = new(GeneratorX, GeneratorY, IsInfinity: false);

    public static BigInteger RandomScalar()
    {
        Span<byte> bytes = stackalloc byte[FieldByteLength];

        while (true)
        {
            RandomNumberGenerator.Fill(bytes);
            var scalar = FromUnsignedBigEndian(bytes);

            if (scalar > 0 && scalar < Order)
            {
                return scalar;
            }
        }
    }

    public static BigInteger HashToScalar(string password)
    {
        var input = Encoding.UTF8.GetBytes(password);
        var digest = SHA512.HashData(
            OpaqueEncoding.Concat(OpaqueConstants.HashToScalarLabel, input)
        );

        return PositiveMod(FromUnsignedBigEndian(digest), Order - 1) + 1;
    }

    public static P256Point ParsePoint(string pointBase64, string label)
    {
        return ParsePoint(OpaqueEncoding.FromBase64(pointBase64, label), label);
    }

    public static P256Point ParsePoint(byte[] pointBytes, string label)
    {
        if (
            pointBytes.Length != 1 + FieldByteLength * 2
            || pointBytes[0] != UncompressedPointPrefix
        )
        {
            throw new OpaqueProtocolException($"{label} must be an uncompressed P-256 point.");
        }

        var x = FromUnsignedBigEndian(pointBytes.AsSpan(1, FieldByteLength));
        var y = FromUnsignedBigEndian(pointBytes.AsSpan(1 + FieldByteLength, FieldByteLength));
        var point = new P256Point(x, y, IsInfinity: false);

        if (!IsValidPoint(point))
        {
            throw new OpaqueProtocolException($"{label} is not on the P-256 curve.");
        }

        return point;
    }

    public static byte[] SerializePoint(P256Point point)
    {
        if (point.IsInfinity)
        {
            throw new OpaqueProtocolException("Cannot serialize infinity.");
        }

        var result = new byte[1 + FieldByteLength * 2];
        result[0] = UncompressedPointPrefix;
        ToFixedBigEndian(point.X).CopyTo(result.AsSpan(1));
        ToFixedBigEndian(point.Y).CopyTo(result.AsSpan(1 + FieldByteLength));

        return result;
    }

    public static string SerializePointBase64(P256Point point)
    {
        return OpaqueEncoding.ToBase64(SerializePoint(point));
    }

    public static byte[] ToFixedScalar(BigInteger scalar)
    {
        var normalized = PositiveMod(scalar, Order);

        if (normalized == 0)
        {
            throw new OpaqueProtocolException("Scalar must be non-zero.");
        }

        return ToFixedBigEndian(normalized);
    }

    public static string ToScalarBase64(BigInteger scalar)
    {
        return OpaqueEncoding.ToBase64(ToFixedScalar(scalar));
    }

    public static BigInteger ParseScalarBase64(string scalarBase64, string label)
    {
        var bytes = OpaqueEncoding.FromBase64(scalarBase64, label);

        if (bytes.Length != FieldByteLength)
        {
            throw new OpaqueProtocolException($"{label} must be a 32-byte scalar.");
        }

        var scalar = FromUnsignedBigEndian(bytes);

        if (scalar <= 0 || scalar >= Order)
        {
            throw new OpaqueProtocolException($"{label} is outside the P-256 scalar range.");
        }

        return scalar;
    }

    public static P256Point ScalarMultiply(BigInteger scalar, P256Point point)
    {
        var k = PositiveMod(scalar, Order);

        if (k == 0 || point.IsInfinity)
        {
            return P256Point.Infinity;
        }

        var result = P256Point.Infinity;
        var addend = point;

        while (k > 0)
        {
            if (!k.IsEven)
            {
                result = Add(result, addend);
            }

            addend = Add(addend, addend);
            k >>= 1;
        }

        return result;
    }

    public static byte[] XCoordinate(P256Point point)
    {
        if (point.IsInfinity)
        {
            throw new OpaqueProtocolException("Shared point was infinity.");
        }

        return ToFixedBigEndian(point.X);
    }

    public static bool IsValidPoint(P256Point point)
    {
        if (point.IsInfinity)
        {
            return false;
        }

        if (point.X < 0 || point.X >= Prime || point.Y < 0 || point.Y >= Prime)
        {
            return false;
        }

        var left = PositiveMod(point.Y * point.Y, Prime);
        var right = PositiveMod(point.X * point.X * point.X - 3 * point.X + CurveB, Prime);

        return left == right;
    }

    public static BigInteger InvertScalar(BigInteger scalar)
    {
        return BigInteger.ModPow(PositiveMod(scalar, Order), Order - 2, Order);
    }

    private static P256Point Add(P256Point left, P256Point right)
    {
        if (left.IsInfinity)
        {
            return right;
        }

        if (right.IsInfinity)
        {
            return left;
        }

        if (left.X == right.X)
        {
            if (PositiveMod(left.Y + right.Y, Prime) == 0)
            {
                return P256Point.Infinity;
            }

            return Double(left);
        }

        var lambda = PositiveMod((right.Y - left.Y) * InvertField(right.X - left.X), Prime);
        var x = PositiveMod(lambda * lambda - left.X - right.X, Prime);
        var y = PositiveMod(lambda * (left.X - x) - left.Y, Prime);

        return new P256Point(x, y, IsInfinity: false);
    }

    private static P256Point Double(P256Point point)
    {
        if (point.IsInfinity || point.Y == 0)
        {
            return P256Point.Infinity;
        }

        var lambda = PositiveMod((3 * point.X * point.X - 3) * InvertField(2 * point.Y), Prime);
        var x = PositiveMod(lambda * lambda - 2 * point.X, Prime);
        var y = PositiveMod(lambda * (point.X - x) - point.Y, Prime);

        return new P256Point(x, y, IsInfinity: false);
    }

    private static BigInteger InvertField(BigInteger value)
    {
        return BigInteger.ModPow(PositiveMod(value, Prime), Prime - 2, Prime);
    }

    private static BigInteger PositiveMod(BigInteger value, BigInteger modulus)
    {
        var result = value % modulus;

        return result < 0 ? result + modulus : result;
    }

    private static BigInteger FromHex(string value)
    {
        return BigInteger.Parse(
            $"00{value}",
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture
        );
    }

    private static BigInteger FromUnsignedBigEndian(ReadOnlySpan<byte> bytes)
    {
        return new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
    }

    private static byte[] ToFixedBigEndian(BigInteger value)
    {
        var bytes = value.ToByteArray(isUnsigned: true, isBigEndian: true);

        if (bytes.Length > FieldByteLength)
        {
            throw new OpaqueProtocolException("Value is too large.");
        }

        if (bytes.Length == FieldByteLength)
        {
            return bytes;
        }

        var result = new byte[FieldByteLength];
        bytes.CopyTo(result.AsSpan(FieldByteLength - bytes.Length));

        return result;
    }
}

internal readonly record struct P256Point(BigInteger X, BigInteger Y, bool IsInfinity)
{
    public static P256Point Infinity { get; } =
        new(BigInteger.Zero, BigInteger.Zero, IsInfinity: true);
}
