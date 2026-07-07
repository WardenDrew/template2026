using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using NodaTime;

namespace TemplateApi.Data;

[Table("users")]
[Index(nameof(NormalizedEmail), IsUnique = true)]
public sealed class User
{
    /// <summary>
    /// Unique identifier for the user.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User email address or another stable login identifier.
    /// </summary>
    [Required]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Normalized email address used for lookup and uniqueness.
    /// </summary>
    [Required]
    [MaxLength(320)]
    public string NormalizedEmail { get; set; } = string.Empty;

    /// <summary>
    /// User display name.
    /// </summary>
    [MaxLength(200)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Account lifecycle state, such as active or disabled.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "active";

    /// <summary>
    /// Timestamp when the user was created.
    /// </summary>
    public Instant CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the account completed registration.
    /// </summary>
    public Instant? RegisteredAt { get; set; }

    /// <summary>
    /// Timestamp when the user was disabled.
    /// </summary>
    public Instant? DisabledAt { get; set; }
}
