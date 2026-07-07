using FastEndpoints;

namespace TemplateApi.Jobs;

public sealed class NotificationJob : ICommand
{
    public Guid UserId { get; init; }

    public string Subject { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;
}
