using System.Text;

namespace Template.Opaque;

internal static class OpaqueConstants
{
    public const int Version = 1;
    public const string Profile = "TEMPLATE-OPAQUE-P256-SHA256-V1";

    public static readonly byte[] HashToScalarLabel = Utf8($"{Profile}:hash-to-scalar");
    public static readonly byte[] OprfFinalizeLabel = Utf8($"{Profile}:oprf-finalize");
    public static readonly byte[] SessionLabel = Utf8($"{Profile}:session");
    public static readonly byte[] ClientMacLabel = Utf8($"{Profile}:client-finish");

    private static byte[] Utf8(string value)
    {
        return Encoding.UTF8.GetBytes(value);
    }
}
