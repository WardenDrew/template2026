namespace TemplateApi.Models;

public sealed record ResourceDto(Guid Id, string Name, string? Description, string CreatedAt);
