using TemplateApi.Jobs;

namespace TemplateApi.Services;

public sealed class NotificationService
{
    public Task ExecuteJobAsync(NotificationJob job, CancellationToken ct)
    {
        // Put provider-specific delivery logic here.
        return Task.CompletedTask;
    }
}
