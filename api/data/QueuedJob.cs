using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace TemplateApi.Data;

[Table("queued_jobs")]
[Index(
    nameof(QueueID),
    nameof(IsComplete),
    nameof(ExecuteAfter),
    nameof(ExpireOn),
    nameof(DequeueAfter)
)]
[Index(nameof(CommandName))]
public sealed class QueuedJob : IJobStorageRecord, IHasCommandType
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new(
        JsonSerializerDefaults.Web
    );

    /// <summary>
    /// Unique identifier used by FastEndpoints to track this queued job.
    /// </summary>
    [Key]
    public Guid TrackingID { get; set; }

    /// <summary>
    /// FastEndpoints queue identifier for the command type.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string QueueID { get; set; } = string.Empty;

    /// <summary>
    /// Short command type name used for filtering and diagnostics.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string CommandName { get; set; } = string.Empty;

    /// <summary>
    /// Fully-qualified command type name.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string CommandType { get; set; } = string.Empty;

    /// <summary>
    /// Serialized command payload.
    /// </summary>
    [Required]
    [Column(TypeName = "jsonb")]
    public string CommandJson { get; set; } = "{}";

    /// <summary>
    /// Deserialized command instance used at runtime.
    /// </summary>
    [NotMapped]
    public object Command { get; set; } = null!;

    /// <summary>
    /// UTC timestamp before which the job should not execute.
    /// </summary>
    public DateTime ExecuteAfter { get; set; }

    /// <summary>
    /// UTC timestamp after which the incomplete job is stale.
    /// </summary>
    public DateTime ExpireOn { get; set; }

    /// <summary>
    /// Indicates whether the job has completed or been canceled.
    /// </summary>
    public bool IsComplete { get; set; }

    /// <summary>
    /// UTC lease timestamp used to prevent concurrent claims.
    /// </summary>
    public DateTime DequeueAfter { get; set; } = DateTime.UnixEpoch;

    /// <summary>
    /// Number of handler failures recorded for this queued job.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int AttemptCount { get; set; }

    /// <summary>
    /// Last recorded handler failure message for diagnostics.
    /// </summary>
    [MaxLength(4000)]
    public string? LastError { get; set; }

    /// <summary>
    /// UTC timestamp when the last handler failure occurred.
    /// </summary>
    public DateTime? LastFailedAt { get; set; }

    public TCommand GetCommand<TCommand>()
        where TCommand : class, ICommandBase
    {
        if (Command is TCommand command)
        {
            return command;
        }

        var deserializedCommand =
            JsonSerializer.Deserialize<TCommand>(CommandJson, JsonSerializerOptions)
            ?? throw new InvalidOperationException(
                $"Queued job payload could not be deserialized as {typeof(TCommand).FullName}."
            );

        Command = deserializedCommand;

        return deserializedCommand;
    }

    public void SetCommand<TCommand>(TCommand command)
        where TCommand : class, ICommandBase
    {
        ArgumentNullException.ThrowIfNull(command);

        Command = command;
        CommandName = typeof(TCommand).Name;
        CommandType =
            typeof(TCommand).AssemblyQualifiedName
            ?? typeof(TCommand).FullName
            ?? typeof(TCommand).Name;
        CommandJson = JsonSerializer.Serialize(command, JsonSerializerOptions);
    }
}
