namespace TransactionsIngest.Api.Domain;

/// <summary>
/// A card transaction imported from a source system.
/// The logical unique key is (SourceSystem, TransactionId).
/// </summary>
public class Transaction
{
    /// <summary>Internal database primary key (surrogate key).</summary>
    public int Id { get; set; }

    public string SourceSystem { get; set; } = string.Empty;

    /// <summary>The source system's own ID. Unique only within a SourceSystem.</summary>
    public int TransactionId { get; set; }

    /// <summary>Masked card (e.g. "***1111"). Empty for non-card payments.</summary>
    public string CardNumber { get; set; } = string.Empty;

    public string LocationCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>When the transaction happened in the source system (UTC).</summary>
    public DateTime TransactionTime { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Active;

    /// <summary>When we first imported this record (UTC).</summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>When an import last changed this record (UTC).</summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Change history for this transaction.</summary>
    public List<TransactionAudit> AuditEntries { get; set; } = new();
}