using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Domain;
using Xunit;
using static TransactionsIngest.Tests.ImportTestHarness;

namespace TransactionsIngest.Tests;

public class ImportServiceTests : IDisposable
{
    private readonly ImportTestHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task FirstImport_InsertsNewTransactions_AndRecordsCreatedAudit()
    {
        _h.Uptown.Snapshot = [Tx("Store Uptown", 1), Tx("Store Uptown", 2)];

        var result = await _h.RunImportAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.Inserted);
        await using var db = _h.CreateDbContext();
        Assert.Equal(2, await db.Transactions.CountAsync());
        Assert.Equal(2, await db.TransactionAudits.CountAsync(a => a.ChangeType == AuditChangeType.Created));
    }

    [Fact]
    public async Task SameTransactionId_InDifferentSources_IsStoredSeparately()
    {
        _h.Uptown.Snapshot = [Tx("Store Uptown", 100001)];
        _h.Online.Snapshot = [Tx("Online", 100001)];

        var result = await _h.RunImportAsync();

        Assert.Equal(2, result.Inserted);
    }

    [Fact]
    public async Task ImportingIdenticalSnapshotTwice_IsIdempotent()
    {
        _h.Uptown.Snapshot = [Tx("Store Uptown", 1), Tx("Store Uptown", 2, hoursAgo: 30)];
        await _h.RunImportAsync();

        int auditCountAfterFirstRun;
        await using (var db = _h.CreateDbContext())
        {
            auditCountAfterFirstRun = await db.TransactionAudits.CountAsync();
        }

        var second = await _h.RunImportAsync();

        Assert.Equal(0, second.Inserted + second.Updated + second.Revoked + second.Restored + second.Finalized);
        Assert.Equal(2, second.Unchanged);
        await using var db2 = _h.CreateDbContext();
        Assert.Equal(auditCountAfterFirstRun, await db2.TransactionAudits.CountAsync());
    }

    [Fact]
    public async Task ChangedFields_AreUpdated_WithOneAuditRowPerField()
    {
        _h.Uptown.Snapshot = [Tx("Store Uptown", 1, product: "Mouse", amount: 24.99m)];
        await _h.RunImportAsync();

        _h.Uptown.Snapshot = [Tx("Store Uptown", 1, product: "Gaming Mouse", amount: 19.99m)];
        var result = await _h.RunImportAsync();

        Assert.Equal(1, result.Updated);
        await using var db = _h.CreateDbContext();
        var transaction = await db.Transactions.SingleAsync();
        Assert.Equal(19.99m, transaction.Amount);
        Assert.Equal("Gaming Mouse", transaction.ProductName);

        var updates = await db.TransactionAudits
            .Where(a => a.ChangeType == AuditChangeType.Updated)
            .OrderBy(a => a.FieldName)
            .ToListAsync();
        Assert.Collection(updates,
            a => { Assert.Equal("Amount", a.FieldName); Assert.Equal("24.99", a.OldValue); Assert.Equal("19.99", a.NewValue); },
            a => { Assert.Equal("ProductName", a.FieldName); Assert.Equal("Mouse", a.OldValue); Assert.Equal("Gaming Mouse", a.NewValue); });
    }

    [Fact]
    public async Task MissingTransaction_WithinWindow_IsRevoked_WithAudit()
    {
        _h.Uptown.Snapshot = [Tx("Store Uptown", 1), Tx("Store Uptown", 2, hoursAgo: 5)];
        await _h.RunImportAsync();

        _h.Uptown.Snapshot = [Tx("Store Uptown", 1)];
        var result = await _h.RunImportAsync();

        Assert.Equal(1, result.Revoked);
        await using var db = _h.CreateDbContext();
        var revoked = await db.Transactions.SingleAsync(t => t.TransactionId == 2);
        Assert.Equal(TransactionStatus.Revoked, revoked.Status);
        Assert.True(await db.TransactionAudits.AnyAsync(a =>
            a.TransactionRecordId == revoked.Id && a.ChangeType == AuditChangeType.Revoked));
    }

    [Fact]
    public async Task MissingTransaction_OlderThanWindow_IsNotRevoked()
    {
        _h.ImportOptions.FinalizationEnabled = false;
        _h.Uptown.Snapshot = [Tx("Store Uptown", 1, hoursAgo: 30)];
        await _h.RunImportAsync();

        _h.Uptown.Snapshot = [];
        var result = await _h.RunImportAsync();

        Assert.Equal(0, result.Revoked);
        await using var db = _h.CreateDbContext();
        Assert.Equal(TransactionStatus.Active, (await db.Transactions.SingleAsync()).Status);
    }

    [Fact]
    public async Task TransactionCrossingTheWindow_IsFinalized_AndThenIgnoresChanges()
    {
        _h.Uptown.Snapshot = [Tx("Store Uptown", 1, amount: 10m, hoursAgo: 20)];
        await _h.RunImportAsync();

        _h.Clock.UtcNow = Now.AddHours(5);   // the transaction is now 25 hours old
        var finalizeRun = await _h.RunImportAsync();
        Assert.Equal(1, finalizeRun.Finalized);

        _h.Uptown.Snapshot = [Tx("Store Uptown", 1, amount: 99m, hoursAgo: 20)];
        var changeRun = await _h.RunImportAsync();
        Assert.Equal(0, changeRun.Updated);

        await using var db = _h.CreateDbContext();
        var transaction = await db.Transactions.SingleAsync();
        Assert.Equal(TransactionStatus.Finalized, transaction.Status);
        Assert.Equal(10m, transaction.Amount);
    }

    [Fact]
    public async Task RevokedTransaction_ThatReappears_IsRestored()
    {
        _h.Uptown.Snapshot = [Tx("Store Uptown", 1)];
        await _h.RunImportAsync();
        _h.Uptown.Snapshot = [];
        await _h.RunImportAsync();

        _h.Uptown.Snapshot = [Tx("Store Uptown", 1)];
        var result = await _h.RunImportAsync();

        Assert.Equal(1, result.Restored);
        await using var db = _h.CreateDbContext();
        Assert.Equal(TransactionStatus.Active, (await db.Transactions.SingleAsync()).Status);
    }

    [Fact]
    public async Task InvalidRecord_IsSkipped_AndDoesNotRevokeExistingTransaction()
    {
        _h.Uptown.Snapshot = [Tx("Store Uptown", 1, product: "Mouse")];
        await _h.RunImportAsync();

        _h.Uptown.Snapshot = [Tx("Store Uptown", 1, product: "A product name that is far too long")];
        var result = await _h.RunImportAsync();

        Assert.Equal(1, result.Skipped);
        Assert.Equal(0, result.Revoked);
        await using var db = _h.CreateDbContext();
        var transaction = await db.Transactions.SingleAsync();
        Assert.Equal(TransactionStatus.Active, transaction.Status);
        Assert.Equal("Mouse", transaction.ProductName);
    }

    [Fact]
    public async Task FailingSource_IsSkippedWithoutRevocations_WhileOtherSourcesStillImport()
    {
        _h.Uptown.Snapshot = [Tx("Store Uptown", 1)];
        await _h.RunImportAsync();

        _h.Uptown.Fail = true;
        _h.Online.Snapshot = [Tx("Online", 7)];
        var result = await _h.RunImportAsync();

        Assert.False(result.Succeeded);
        Assert.Contains("Store Uptown", result.ErrorMessage);
        Assert.Equal(0, result.Revoked);
        Assert.Equal(1, result.Inserted);
        await using var db = _h.CreateDbContext();
        var uptown = await db.Transactions.SingleAsync(t => t.SourceSystem == "Store Uptown");
        Assert.Equal(TransactionStatus.Active, uptown.Status);
    }
}