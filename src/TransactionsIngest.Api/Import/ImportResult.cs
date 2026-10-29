using TransactionsIngest.Api.Domain;

namespace TransactionsIngest.Api.Import;

public sealed record ImportResult(
    int ImportRunId,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    bool Succeeded,
    string? ErrorMessage,
    int Inserted,
    int Updated,
    int Revoked,
    int Restored,
    int Finalized,
    int Unchanged,
    int Skipped)
{
    public static ImportResult FromRun(ImportRun run) => new(
        run.Id, run.StartedAtUtc, run.CompletedAtUtc, run.Succeeded, run.ErrorMessage,
        run.InsertedCount, run.UpdatedCount, run.RevokedCount, run.RestoredCount,
        run.FinalizedCount, run.UnchangedCount, run.SkippedCount);
}