using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Contracts;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Domain;

namespace TransactionsIngest.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController(TransactionsDbContext db) : ControllerBase
{
    /// <summary>Imported transactions, newest first. Optional filters: sourceSystem, status.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TransactionDto>>> GetTransactions(
        [FromQuery] string? sourceSystem,
        [FromQuery] TransactionStatus? status,
        CancellationToken cancellationToken)
    {
        var query = db.Transactions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(sourceSystem))
        {
            query = query.Where(t => t.SourceSystem == sourceSystem);
        }
        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        var items = await query
            .OrderByDescending(t => t.TransactionTime)
            .ThenBy(t => t.SourceSystem)
            .ThenBy(t => t.TransactionId)
            .Select(t => new TransactionDto(
                t.Id, t.SourceSystem, t.TransactionId, t.CardNumber, t.LocationCode,
                t.ProductName, t.Amount, t.TransactionTime, t.Status, t.CreatedAtUtc, t.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(items);
    }

    /// <summary>Audit history for one transaction, oldest first.</summary>
    [HttpGet("{id:int}/audit")]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> GetAuditHistory(int id, CancellationToken cancellationToken)
    {
        if (!await db.Transactions.AnyAsync(t => t.Id == id, cancellationToken))
        {
            return NotFound();
        }

        var entries = await db.TransactionAudits.AsNoTracking()
            .Where(a => a.TransactionRecordId == id)
            .OrderBy(a => a.ChangedAtUtc).ThenBy(a => a.Id)
            .Select(a => new AuditEntryDto(
                a.Id, a.ImportRunId, a.ChangeType, a.FieldName, a.OldValue, a.NewValue, a.ChangedAtUtc))
            .ToListAsync(cancellationToken);

        return Ok(entries);
    }
}