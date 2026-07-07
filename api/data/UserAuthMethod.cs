using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace TemplateApi.Data;

[Table("user_auth_methods")]
[Index(nameof(UserId), nameof(MethodType))]
public sealed class UserAuthMethod
{
    /// <summary>
    /// Unique identifier for this authentication method.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User account that owns this authentication method.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Authentication method type, such as password or totp.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string MethodType { get; set; } = string.Empty;

    /// <summary>
    /// Lifecycle state for this method.
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "active";

    /// <summary>
    /// Server-side verifier data, such as an OPAQUE registration record.
    /// </summary>
    [Column(TypeName = "jsonb")]
    public string? VerifierJson { get; set; }

    /// <summary>
    /// Timestamp when this auth method was created.
    /// </summary>
    public Instant CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this auth method last succeeded.
    /// </summary>
    public Instant? LastUsedAt { get; set; }

    /// <summary>
    /// Timestamp when this auth method was revoked.
    /// </summary>
    public Instant? RevokedAt { get; set; }

    public sealed class Configuration : IEntityTypeConfiguration<UserAuthMethod>
    {
        public void Configure(EntityTypeBuilder<UserAuthMethod> builder)
        {
            builder
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(method => method.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
