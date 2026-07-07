namespace TemplateApi.Options;

public sealed class RegistrationTokenOptions
{
    public const string SectionName = "RegistrationTokens";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string ActiveSigningKeyId { get; set; } = string.Empty;

    public string ActiveEncryptionKeyId { get; set; } = string.Empty;

    public List<TokenKeyOptions> SigningKeys { get; set; } = [];

    public List<TokenKeyOptions> EncryptionKeys { get; set; } = [];

    public int ExpirationMinutes { get; set; } = 15;
}
