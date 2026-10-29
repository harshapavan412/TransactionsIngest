using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Sources;

namespace TransactionsIngest.Api.Import;

public static class SourceTransactionValidator
{
    private const int MaxLength = TransactionsDbContext.ShortTextLength;

    /// <summary>Returns null when the record is valid, otherwise the rejection reason.</summary>
    public static string? Validate(SourceTransaction item, string expectedSourceSystem)
    {
        if (!string.Equals(item.SourceSystem, expectedSourceSystem, StringComparison.Ordinal))
            return $"SourceSystem '{item.SourceSystem}' does not match '{expectedSourceSystem}'";
        if (item.TransactionId <= 0)
            return "TransactionId must be positive";
        if (string.IsNullOrWhiteSpace(item.LocationCode))
            return "LocationCode is required";
        if (string.IsNullOrWhiteSpace(item.ProductName))
            return "ProductName is required";
        if (item.LocationCode.Length > MaxLength)
            return $"LocationCode exceeds {MaxLength} characters";
        if (item.ProductName.Length > MaxLength)
            return $"ProductName exceeds {MaxLength} characters";
        if ((item.CardNumber?.Length ?? 0) > MaxLength)
            return $"CardNumber exceeds {MaxLength} characters";
        if (decimal.Round(item.Amount, 2) != item.Amount)
            return "Amount has more than 2 decimal places";
        if (item.TransactionTime == default)
            return "TransactionTime is required";

        return null;
    }
}