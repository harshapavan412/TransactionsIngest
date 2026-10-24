namespace TransactionsIngest.Api.Sources;

/// <summary>
/// A source system that publishes a full snapshot of its recent transactions.
/// </summary>
public interface ITransactionSource
{
    /// <summary>The source system name, e.g. "Store Uptown".</summary>
    string SourceSystem { get; }

    Task<IReadOnlyList<SourceTransaction>> GetSnapshotAsync(CancellationToken cancellationToken = default);
}