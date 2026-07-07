using Microsoft.EntityFrameworkCore;

namespace TemplateApi.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<UserAuthMethod> UserAuthMethods => Set<UserAuthMethod>();

    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<Resource> Resources => Set<Resource>();

    public DbSet<QueuedJob> QueuedJobs => Set<QueuedJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
