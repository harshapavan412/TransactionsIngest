using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Import;
using TransactionsIngest.Api.Sources;

namespace TransactionsIngest.Tests;

/// <summary>A clock the test controls, so the 24-hour rules can be tested without waiting.</summary>
public sealed class TestClock(DateTime utcNow) : TimeProvider
{
    public DateTime UtcNow { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => new(UtcNow, TimeSpan.Zero);
}

/// <summary>An in-memory source system whose snapshot each test sets directly.</summary>
public sealed class FakeSource(string sourceSystem) : ITransactionSource
{
    public string SourceSystem { get; } = sourceSystem;
    public List<SourceTransaction> Snapshot { get; set; } = [];
    public bool Fail { get; set; }

    public Task<IReadOnlyList<SourceTransaction>> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
        Fail
            ? throw new InvalidOperationException($"{SourceSystem} is unavailable")
            : Task.FromResult<IReadOnlyList<SourceTransaction>>(Snapshot.ToList());
}

/// <summary>
/// Wires the real ImportService to an in-memory SQLite database, fake sources and a fake clock.
/// Each import uses a fresh DbContext, just like each HTTP request does in the app.
/// </summary>
public sealed class ImportTestHarness : IDisposable
{
    public static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

    private readonly SqliteConnection _connection;

    public TestClock Clock { get; } = new(Now);
    public FakeSource Uptown { get; } = new("Store Uptown");
    public FakeSource Online { get; } = new("Online");
    public ImportOptions ImportOptions { get; } = new() { RevocationWindowHours = 24, FinalizationEnabled = true };

    public ImportTestHarness()
    {
        // An in-memory SQLite database lives as long as this connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public TransactionsDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TransactionsDbContext>().UseSqlite(_connection).Options);

    public async Task<ImportResult> RunImportAsync()
    {
        await using var db = CreateDbContext();
        var service = new ImportService(
            db,
            [Uptown, Online],
            Options.Create(ImportOptions),
            Clock,
            NullLogger<ImportService>.Instance);

        return await service.RunImportAsync();
    }

    /// <summary>Builds a source transaction with sensible defaults.</summary>
    public static SourceTransaction Tx(
        string source,
        int id,
        string product = "Widget",
        decimal amount = 10.00m,
        double hoursAgo = 1,
        string card = "***1234") =>
        new(source, id, card, "LOC1", product, amount, Now.AddHours(-hoursAgo));

    public void Dispose() => _connection.Dispose();
}