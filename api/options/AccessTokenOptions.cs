namespace TemplateApi.Options;

public sealed class AccessTokenOptions
{
    public const string SectionName = "AccessTokens";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string ActiveSigningKeyId { get; set; } = string.Empty;

    public List<TokenKeyOptions> SigningKeys { get; set; } = [];
}
