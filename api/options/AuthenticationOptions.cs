namespace TemplateApi.Options;

public sealed class AuthenticationOptions
{
    public const string SectionName = "Authentication";

    public string OpaqueActiveKeyId { get; set; } = string.Empty;

    public List<OpaqueServerKeyOptions> OpaqueServerKeys { get; set; } = [];
}
