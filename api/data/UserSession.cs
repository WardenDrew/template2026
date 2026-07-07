using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace TemplateApi.Data;

[Table("user_sessions")]
[Index(nameof(UserId), nameof(RevokedAt), nameof(ExpiresAt))]
public sealed class UserSession
{
    /// <summary>
    /// Unique identifier for this login session.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User account that owns this session.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Timestamp when the session was created.
    /// </summary>
    public Instant CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the session expires.
    /// </summary>
    public Instant ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp when this session last authenticated a request.
    /// </summary>
    public Instant? LastUsedAt { get; set; }

    /// <summary>
    /// Timestamp when this session was revoked by logout.
    /// </summary>
    public Instant? RevokedAt { get; set; }

    public sealed class Configuration : IEntityTypeConfiguration<UserSession>
    {
        public void Configure(EntityTypeBuilder<UserSession> builder)
        {
            builder
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(session => session.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
