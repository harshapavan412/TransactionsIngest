using TransactionsIngest.Api.Domain;

namespace TransactionsIngest.Api.Contracts;

/// <summary>What the API returns for a transaction (decoupled from the database entity).</summary>
public sealed record TransactionDto(
    int Id,
    string SourceSystem,
    int TransactionId,
    string CardNumber,
    string LocationCode,
    string ProductName,
    decimal Amount,
    DateTime TransactionTime,
    TransactionStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AuditEntryDto(
    int Id,
    int ImportRunId,
    AuditChangeType ChangeType,
    string? FieldName,
    string? OldValue,
    string? NewValue,
    DateTime ChangedAtUtc);