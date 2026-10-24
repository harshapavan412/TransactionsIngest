using System.ComponentModel.DataAnnotations;

namespace TransactionsIngest.Api.Import;

/// <summary>
/// Bound from the "Import" section of appsettings.json.
/// </summary>
public class ImportOptions
{
    public const string SectionName = "Import";

    /// <summary>How long after TransactionTime a missing transaction counts as revoked.</summary>
    [Range(1, 720)]
    public int RevocationWindowHours { get; set; } = 24;

    /// <summary>When true, transactions older than the window are locked as Finalized.</summary>
    public bool FinalizationEnabled { get; set; } = true;
}