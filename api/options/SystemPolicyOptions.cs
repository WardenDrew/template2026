namespace TemplateApi.Options;

public sealed class SystemPolicyOptions
{
    public const string SectionName = "SystemPolicy";

    public int SessionExpirationMinutes { get; set; } = 1440;
}
