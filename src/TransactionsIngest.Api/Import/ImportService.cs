using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Domain;
using TransactionsIngest.Api.Sources;

namespace TransactionsIngest.Api.Import;

/// <summary>
/// Pulls a snapshot from every source system and reconciles it with the database:
/// inserts new transactions, updates changed ones (with field-level audit),
/// revokes recent ones that disappeared, restores ones that came back,
/// and finalizes ones older than the revocation window.
/// </summary>
public sealed class ImportService(
    TransactionsDbContext db,
    IEnumerable<ITransactionSource> sources,
    IOptions<ImportOptions> options,
    TimeProvider timeProvider,
    ILogger<ImportService> logger) : IImportService
{
    // One import at a time per process. A second request waits for the first to finish.
    private static readonly SemaphoreSlim ImportLock = new(1, 1);

    private readonly ImportOptions _options = options.Value;

    public async Task<ImportResult> RunImportAsync(CancellationToken cancellationToken = default)
    {
        await ImportLock.WaitAsync(cancellationToken);
        try
        {
            return await RunImportCoreAsync(cancellationToken);
        }
        finally
        {
            ImportLock.Release();
        }
    }

    private async Task<ImportResult> RunImportCoreAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var windowStart = now.AddHours(-_options.RevocationWindowHours);

        // Record the run first, so even a failed run leaves a trace.
        var run = new ImportRun { StartedAtUtc = now };
        db.ImportRuns.Add(run);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Import run {ImportRunId} started", run.Id);

        try
        {
            // All data changes in one transaction: they are saved together or not at all.
            await using var dbTransaction = await db.Database.BeginTransactionAsync(ct);
            var sourceErrors = new List<string>();

            foreach (var source in sources)
            {
                IReadOnlyList<SourceTransaction> snapshot;
                try
                {
                    snapshot = await source.GetSnapshotAsync(ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // An unavailable source is skipped. It must never cause revocations.
                    logger.LogError(ex, "Could not retrieve snapshot from {SourceSystem}; source skipped", source.SourceSystem);
                    sourceErrors.Add($"{source.SourceSystem}: {ex.Message}");
                    continue;
                }

                await ReconcileSourceAsync(source.SourceSystem, snapshot, run, now, windowStart, ct);
                await db.SaveChangesAsync(ct);
            }

            if (_options.FinalizationEnabled)
            {
                await FinalizeOldTransactionsAsync(run, now, windowStart, ct);
            }

            run.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            run.Succeeded = sourceErrors.Count == 0;
            run.ErrorMessage = sourceErrors.Count == 0 ? null : Truncate(string.Join("; ", sourceErrors));

            await db.SaveChangesAsync(ct);
            await dbTransaction.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import run {ImportRunId} failed and was rolled back", run.Id);
            await MarkRunFailedAsync(run.Id, ex);
            throw;
        }

        logger.LogInformation(
            "Import run {ImportRunId} completed: {Inserted} inserted, {Updated} updated, {Revoked} revoked, " +
            "{Restored} restored, {Finalized} finalized, {Unchanged} unchanged, {Skipped} skipped",
            run.Id, run.InsertedCount, run.UpdatedCount, run.RevokedCount,
            run.RestoredCount, run.FinalizedCount, run.UnchangedCount, run.SkippedCount);

        return ImportResult.FromRun(run);
    }

    private async Task ReconcileSourceAsync(
        string sourceSystem,
        IReadOnlyList<SourceTransaction> snapshot,
        ImportRun run,
        DateTime now,
        DateTime windowStart,
        CancellationToken ct)
    {
        // Every ID the source published, valid or not. A record rejected by validation
        // is still present in the source, so it must not be treated as revoked.
        var seenIds = snapshot.Select(s => s.TransactionId).ToHashSet();

        var incoming = new Dictionary<int, SourceTransaction>();
        foreach (var item in snapshot)
        {
            var error = SourceTransactionValidator.Validate(item, sourceSystem);
            if (error is not null)
            {
                logger.LogWarning("Skipped {SourceSystem} transaction {TransactionId}: {Reason}",
                    sourceSystem, item.TransactionId, error);
                run.SkippedCount++;
                continue;
            }

            if (!incoming.TryAdd(item.TransactionId, Normalize(item)))
            {
                logger.LogWarning("Skipped duplicate {SourceSystem} transaction {TransactionId} in snapshot",
                    sourceSystem, item.TransactionId);
                run.SkippedCount++;
            }
        }

        // Load only what this run can affect: records in the snapshot, plus Active ones (revocation candidates).
        var ids = incoming.Keys.ToList();
        var existing = await db.Transactions
            .Where(t => t.SourceSystem == sourceSystem
                        && (ids.Contains(t.TransactionId) || t.Status == TransactionStatus.Active))
            .ToDictionaryAsync(t => t.TransactionId, ct);

        foreach (var item in incoming.Values)
        {
            if (existing.TryGetValue(item.TransactionId, out var current))
            {
                ApplyChanges(current, item, run, now);
            }
            else
            {
                Insert(item, run, now);
            }
        }

        foreach (var current in existing.Values)
        {
            var missing = !seenIds.Contains(current.TransactionId);
            var withinWindow = current.TransactionTime >= windowStart;

            if (current.Status == TransactionStatus.Active && missing && withinWindow)
            {
                ChangeStatus(current, TransactionStatus.Revoked, AuditChangeType.Revoked, run, now);
                run.RevokedCount++;
            }
        }
    }

    private void Insert(SourceTransaction item, ImportRun run, DateTime now)
    {
        var transaction = new Transaction
        {
            SourceSystem = item.SourceSystem,
            TransactionId = item.TransactionId,
            CardNumber = item.CardNumber ?? string.Empty,
            LocationCode = item.LocationCode,
            ProductName = item.ProductName,
            Amount = item.Amount,
            TransactionTime = item.TransactionTime,
            Status = TransactionStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        transaction.AuditEntries.Add(new TransactionAudit
        {
            ImportRun = run,
            ChangeType = AuditChangeType.Created,
            ChangedAtUtc = now
        });

        db.Transactions.Add(transaction);
        run.InsertedCount++;
    }

    private void ApplyChanges(Transaction current, SourceTransaction item, ImportRun run, DateTime now)
    {
        var changes = GetFieldChanges(current, item);

        if (current.Status == TransactionStatus.Finalized)
        {
            // Finalized transactions are immutable.
            if (changes.Count > 0)
            {
                logger.LogWarning("Ignored {Count} change(s) to finalized {SourceSystem} transaction {TransactionId}",
                    changes.Count, current.SourceSystem, current.TransactionId);
            }
            run.UnchangedCount++;
            return;
        }

        if (changes.Count > 0)
        {
            current.CardNumber = item.CardNumber ?? string.Empty;
            current.LocationCode = item.LocationCode;
            current.ProductName = item.ProductName;
            current.Amount = item.Amount;
            current.TransactionTime = item.TransactionTime;
            current.UpdatedAtUtc = now;

            foreach (var (field, oldValue, newValue) in changes)
            {
                current.AuditEntries.Add(new TransactionAudit
                {
                    ImportRun = run,
                    ChangeType = AuditChangeType.Updated,
                    FieldName = field,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedAtUtc = now
                });
            }
            run.UpdatedCount++;
        }

        if (current.Status == TransactionStatus.Revoked)
        {
            ChangeStatus(current, TransactionStatus.Active, AuditChangeType.Restored, run, now);
            run.RestoredCount++;
        }
        else if (changes.Count == 0)
        {
            run.UnchangedCount++;
        }
    }

    private async Task FinalizeOldTransactionsAsync(ImportRun run, DateTime now, DateTime windowStart, CancellationToken ct)
    {
        var expired = await db.Transactions
            .Where(t => t.Status == TransactionStatus.Active && t.TransactionTime < windowStart)
            .ToListAsync(ct);

        foreach (var transaction in expired)
        {
            ChangeStatus(transaction, TransactionStatus.Finalized, AuditChangeType.Finalized, run, now);
        }
        run.FinalizedCount = expired.Count;
    }

    private static void ChangeStatus(
        Transaction transaction,
        TransactionStatus newStatus,
        AuditChangeType changeType,
        ImportRun run,
        DateTime now)
    {
        transaction.AuditEntries.Add(new TransactionAudit
        {
            ImportRun = run,
            ChangeType = changeType,
            FieldName = nameof(Transaction.Status),
            OldValue = transaction.Status.ToString(),
            NewValue = newStatus.ToString(),
            ChangedAtUtc = now
        });
        transaction.Status = newStatus;
        transaction.UpdatedAtUtc = now;
    }

    private static List<(string Field, string OldValue, string NewValue)> GetFieldChanges(
        Transaction current, SourceTransaction item)
    {
        var changes = new List<(string Field, string OldValue, string NewValue)>();

        void Compare<T>(string field, T oldValue, T newValue)
        {
            if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
            {
                changes.Add((field, Format(oldValue), Format(newValue)));
            }
        }

        Compare(nameof(Transaction.CardNumber), current.CardNumber, item.CardNumber ?? string.Empty);
        Compare(nameof(Transaction.LocationCode), current.LocationCode, item.LocationCode);
        Compare(nameof(Transaction.ProductName), current.ProductName, item.ProductName);
        Compare(nameof(Transaction.Amount), current.Amount, item.Amount);
        Compare(nameof(Transaction.TransactionTime), current.TransactionTime, item.TransactionTime);

        return changes;
    }

    private static string Format<T>(T value) => value switch
    {
        decimal d => d.ToString("0.00", CultureInfo.InvariantCulture),
        DateTime dt => dt.ToString("O", CultureInfo.InvariantCulture),
        null => string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static SourceTransaction Normalize(SourceTransaction item) => item with
    {
        CardNumber = item.CardNumber?.Trim() ?? string.Empty,
        LocationCode = item.LocationCode.Trim(),
        ProductName = item.ProductName.Trim(),
        TransactionTime = item.TransactionTime.Kind switch
        {
            DateTimeKind.Utc => item.TransactionTime,
            DateTimeKind.Local => item.TransactionTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(item.TransactionTime, DateTimeKind.Utc)
        }
    };

    private async Task MarkRunFailedAsync(int runId, Exception ex)
    {
        db.ChangeTracker.Clear();
        var failedRun = await db.ImportRuns.FindAsync(runId);
        if (failedRun is null)
        {
            return;
        }

        failedRun.Succeeded = false;
        failedRun.CompletedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        failedRun.ErrorMessage = Truncate(ex.Message);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private static string Truncate(string value) => value.Length <= 2000 ? value : value[..2000];
}