namespace TransactionsIngest.Api.Domain;

/// <summary>
/// One audit history entry. Field-level updates produce one row per changed field.
/// </summary>
public class TransactionAudit
{
    public int Id { get; set; }

    // Points to Transaction.Id (our internal key), not the source system's TransactionId.
    public int TransactionRecordId { get; set; }
    public Transaction Transaction { get; set; } = null!;

    public int ImportRunId { get; set; }
    public ImportRun ImportRun { get; set; } = null!;

    public AuditChangeType ChangeType { get; set; }

    // Only set for Updated entries, e.g. FieldName = "Amount", OldValue = "24.99", NewValue = "19.99".
    public string? FieldName { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }

    public DateTime ChangedAtUtc { get; set; }
}