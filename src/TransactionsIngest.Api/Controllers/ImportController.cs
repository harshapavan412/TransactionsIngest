using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Import;

namespace TransactionsIngest.Api.Controllers;

[ApiController]
[Route("api/import")]
public class ImportController(IImportService importService, TransactionsDbContext db) : ControllerBase
{
    /// <summary>Runs the transaction import manually. The hourly scheduler would call the same service.</summary>
    [HttpPost]
    public async Task<ActionResult<ImportResult>> RunImport(CancellationToken cancellationToken)
    {
        var result = await importService.RunImportAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>The 20 most recent import runs.</summary>
    [HttpGet("runs")]
    public async Task<ActionResult<IReadOnlyList<ImportResult>>> GetRecentRuns(CancellationToken cancellationToken)
    {
        var runs = await db.ImportRuns.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        return Ok(runs.Select(ImportResult.FromRun).ToList());
    }
}