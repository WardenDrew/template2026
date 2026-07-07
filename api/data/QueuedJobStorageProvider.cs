using FastEndpoints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TemplateApi.Options;

namespace TemplateApi.Data;

public sealed class QueuedJobStorageProvider(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IOptions<TemplateApi.Options.JobQueueOptions> options
) : IJobStorageProvider<QueuedJob>
{
    private const int MaxErrorLength = 4000;
    private static readonly DateTime ReadyForDequeue = DateTime.UnixEpoch;
    private readonly TemplateApi.Options.JobQueueOptions options = options.Value;

    public bool DistributedJobProcessingEnabled => true;

    public async Task StoreJobAsync(QueuedJob r, CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        dbContext.QueuedJobs.Add(r);

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<ICollection<QueuedJob>> GetNextBatchAsync(
        PendingJobSearchParams<QueuedJob> parameters
    )
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            parameters.CancellationToken
        );

        var now = DateTime.UtcNow;
        var leaseUntil = now.Add(GetLeaseDuration(parameters));

        return await dbContext
            .QueuedJobs.FromSqlInterpolated(
                $"""
                WITH claimed AS (
                    SELECT "TrackingID"
                    FROM "queued_jobs"
                    WHERE "QueueID" = {parameters.QueueID}
                      AND "IsComplete" = FALSE
                      AND "ExecuteAfter" <= {now}
                      AND "ExpireOn" >= {now}
                      AND "DequeueAfter" <= {now}
                    ORDER BY "ExecuteAfter", "TrackingID"
                    FOR UPDATE SKIP LOCKED
                    LIMIT {parameters.Limit}
                )
                UPDATE "queued_jobs" AS q
                SET "DequeueAfter" = {leaseUntil}
                FROM claimed
                WHERE q."TrackingID" = claimed."TrackingID"
                RETURNING q."TrackingID",
                          q."QueueID",
                          q."CommandName",
                          q."CommandType",
                          q."CommandJson",
                          q."ExecuteAfter",
                          q."ExpireOn",
                          q."IsComplete",
                          q."DequeueAfter",
                          q."AttemptCount",
                          q."LastError",
                          q."LastFailedAt";
                """
            )
            .AsNoTracking()
            .ToListAsync(parameters.CancellationToken);
    }

    public async Task MarkJobAsCompleteAsync(QueuedJob r, CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        await dbContext
            .QueuedJobs.Where(job => job.TrackingID == r.TrackingID)
            .ExecuteUpdateAsync(setters => setters.SetProperty(job => job.IsComplete, true), ct);
    }

    public async Task CancelJobAsync(Guid trackingId, CancellationToken ct)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        await dbContext
            .QueuedJobs.Where(job => job.TrackingID == trackingId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(job => job.IsComplete, true), ct);
    }

    public async Task OnHandlerExecutionFailureAsync(
        QueuedJob r,
        Exception exception,
        CancellationToken ct
    )
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(ct);

        var retryAfter = DateTime.UtcNow.AddSeconds(options.FailureRetryDelaySeconds);
        var failedAt = DateTime.UtcNow;
        var lastError = Truncate(exception.ToString(), MaxErrorLength);

        await dbContext
            .QueuedJobs.Where(job => job.TrackingID == r.TrackingID)
            .ExecuteUpdateAsync(
                setters =>
                    setters
                        .SetProperty(job => job.ExecuteAfter, retryAfter)
                        .SetProperty(job => job.DequeueAfter, ReadyForDequeue)
                        .SetProperty(job => job.AttemptCount, job => job.AttemptCount + 1)
                        .SetProperty(job => job.LastError, lastError)
                        .SetProperty(job => job.LastFailedAt, failedAt),
                ct
            );
    }

    public async Task PurgeStaleJobsAsync(StaleJobSearchParams<QueuedJob> parameters)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(
            parameters.CancellationToken
        );

        var now = DateTime.UtcNow;

        await dbContext
            .QueuedJobs.Where(job => job.ExpireOn < now)
            .ExecuteDeleteAsync(parameters.CancellationToken);
    }

    private TimeSpan GetLeaseDuration(PendingJobSearchParams<QueuedJob> parameters)
    {
        if (
            parameters.ExecutionTimeLimit > TimeSpan.Zero
            && parameters.ExecutionTimeLimit != Timeout.InfiniteTimeSpan
        )
        {
            return parameters.ExecutionTimeLimit.Add(TimeSpan.FromMinutes(1));
        }

        return TimeSpan.FromSeconds(options.LeaseDurationSeconds);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
