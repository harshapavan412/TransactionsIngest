namespace TransactionsIngest.Api.Import;

public interface IImportService
{
    Task<ImportResult> RunImportAsync(CancellationToken cancellationToken = default);
}