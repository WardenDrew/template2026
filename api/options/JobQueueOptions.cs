namespace TemplateApi.Options;

public sealed class JobQueueOptions
{
    public const string SectionName = "JobQueues";

    public int MaxConcurrency { get; init; } = 4;

    public int StorageProbeDelaySeconds { get; init; } = 30;

    public int RetryDelaySeconds { get; init; } = 5;

    public int ExecutionTimeLimitSeconds { get; init; } = 300;

    public int FailureRetryDelaySeconds { get; init; } = 60;

    public int LeaseDurationSeconds { get; init; } = 300;
}
