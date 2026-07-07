using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NodaTime;

namespace TemplateApi.Data;

[Table("resources")]
[Index(nameof(Name))]
public sealed class Resource
{
    /// <summary>
    /// Unique identifier for the resource.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// User-visible resource name.
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional resource description.
    /// </summary>
    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>
    /// User who created this resource.
    /// </summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>
    /// Timestamp when this resource was created.
    /// </summary>
    public Instant CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this resource was deleted.
    /// </summary>
    public Instant? DeletedAt { get; set; }

    public sealed class Configuration : IEntityTypeConfiguration<Resource>
    {
        public void Configure(EntityTypeBuilder<Resource> builder)
        {
            builder
                .HasOne<User>()
                .WithMany()
                .HasForeignKey(resource => resource.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
