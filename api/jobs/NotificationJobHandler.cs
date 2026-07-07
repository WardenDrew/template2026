using FastEndpoints;
using TemplateApi.Services;

namespace TemplateApi.Jobs;

public sealed class NotificationJobHandler(NotificationService notificationService)
    : ICommandHandler<NotificationJob>
{
    public Task ExecuteAsync(NotificationJob command, CancellationToken ct)
    {
        return notificationService.ExecuteJobAsync(command, ct);
    }
}
