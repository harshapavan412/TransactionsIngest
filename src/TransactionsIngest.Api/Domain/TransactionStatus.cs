namespace TransactionsIngest.Api.Domain;

/// <summary>
/// Lifecycle state of an imported transaction.
/// </summary>
public enum TransactionStatus
{
    /// <summary>Present in the latest source snapshot (or still within the 24-hour window).</summary>
    Active = 0,

    /// <summary>Disappeared from the source snapshot within 24 hours of its TransactionTime.</summary>
    Revoked = 1,

    /// <summary>Older than 24 hours; locked and no longer changed by imports.</summary>
    Finalized = 2
}