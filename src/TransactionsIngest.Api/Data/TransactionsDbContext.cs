using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Domain;

namespace TransactionsIngest.Api.Data;

public class TransactionsDbContext(DbContextOptions<TransactionsDbContext> options) : DbContext(options)
{
    public const int ShortTextLength = 20;

    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionAudit> TransactionAudits => Set<TransactionAudit>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transaction>(e =>
        {
            e.HasKey(t => t.Id);

            // The business key: a TransactionId is only unique within its source system.
            e.HasIndex(t => new { t.SourceSystem, t.TransactionId }).IsUnique();
            e.HasIndex(t => t.Status);

            e.Property(t => t.SourceSystem).HasMaxLength(ShortTextLength).IsRequired();
            e.Property(t => t.CardNumber).HasMaxLength(ShortTextLength);
            e.Property(t => t.LocationCode).HasMaxLength(ShortTextLength).IsRequired();
            e.Property(t => t.ProductName).HasMaxLength(ShortTextLength).IsRequired();
            e.Property(t => t.Amount).HasPrecision(18, 2);
            e.Property(t => t.Status).HasConversion<string>().HasMaxLength(ShortTextLength);
        });

        modelBuilder.Entity<TransactionAudit>(e =>
        {
            e.HasKey(a => a.Id);

            e.HasOne(a => a.Transaction)
                .WithMany(t => t.AuditEntries)
                .HasForeignKey(a => a.TransactionRecordId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(a => a.ImportRun)
                .WithMany(r => r.AuditEntries)
                .HasForeignKey(a => a.ImportRunId)
                .OnDelete(DeleteBehavior.Restrict);

            e.Property(a => a.ChangeType).HasConversion<string>().HasMaxLength(ShortTextLength);
            e.Property(a => a.FieldName).HasMaxLength(50);
            e.Property(a => a.OldValue).HasMaxLength(200);
            e.Property(a => a.NewValue).HasMaxLength(200);
        });

        modelBuilder.Entity<ImportRun>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.ErrorMessage).HasMaxLength(2000);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Apply the UTC fix to every DateTime column in one place.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
    }
}