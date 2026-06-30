using Tyto.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Tyto.Api.Infrastructure.Data;

public class TytoDbContext : DbContext
{
    public TytoDbContext(DbContextOptions<TytoDbContext> options) : base(options) { }

    public DbSet<LanguageModel> LanguageModels => Set<LanguageModel>();
    public DbSet<DocumentModel> DocumentModels => Set<DocumentModel>();
    public DbSet<DatabaseConnection> DatabaseConnections => Set<DatabaseConnection>();
    public DbSet<Configuration> Configurations => Set<Configuration>();
    public DbSet<MappedField> MappedFields => Set<MappedField>();
    public DbSet<RunHistory> RunHistories => Set<RunHistory>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TytoDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State == EntityState.Modified))
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
