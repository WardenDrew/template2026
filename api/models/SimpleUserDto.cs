namespace TemplateApi.Models;

public sealed record SimpleUserDto(Guid Id, string Email, string? DisplayName, string Status);
