namespace TransactionsIngest.Api.Domain;

/// <summary>
/// One execution of the import process, kept for traceability.
/// </summary>
public class ImportRun
{
    public int Id { get; set; }

    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }   // null while running

    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }

    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int RevokedCount { get; set; }
    public int RestoredCount { get; set; }
    public int FinalizedCount { get; set; }
    public int UnchangedCount { get; set; }

    public List<TransactionAudit> AuditEntries { get; set; } = new();
}