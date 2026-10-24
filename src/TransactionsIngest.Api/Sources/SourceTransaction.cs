namespace TransactionsIngest.Api.Sources;

/// <summary>
/// A transaction exactly as a source system publishes it (matches the source JSON shape).
/// </summary>
public sealed record SourceTransaction(
    string SourceSystem,
    int TransactionId,
    string? CardNumber,
    string LocationCode,
    string ProductName,
    decimal Amount,
    DateTime TransactionTime);