using System.Text.Json;

namespace TransactionsIngest.Api.Sources;

/// <summary>
/// Mock source system that reads its snapshot from a JSON file on every call,
/// so the file can be edited between imports to simulate corrections and removals.
/// </summary>
public sealed class JsonFileTransactionSource(
    string sourceSystem,
    string filePath,
    ILogger<JsonFileTransactionSource> logger) : ITransactionSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public string SourceSystem { get; } = sourceSystem;

    public async Task<IReadOnlyList<SourceTransaction>> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        // A missing file is an error, not an empty snapshot.
        // Treating it as empty would wrongly revoke every recent transaction from this source.
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Snapshot file for '{SourceSystem}' was not found.", filePath);
        }

        await using var stream = File.OpenRead(filePath);
        var items = await JsonSerializer.DeserializeAsync<List<SourceTransaction>>(stream, JsonOptions, cancellationToken)
                    ?? [];

        logger.LogInformation("Read {Count} transactions from {SourceSystem} snapshot", items.Count, SourceSystem);
        return items;
    }
}