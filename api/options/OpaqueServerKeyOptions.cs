namespace TemplateApi.Options;

public sealed class OpaqueServerKeyOptions
{
    public string KeyId { get; set; } = string.Empty;

    public string PrivateKeyBase64 { get; set; } = string.Empty;
}
