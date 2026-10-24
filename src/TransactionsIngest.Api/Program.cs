using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Import;
using TransactionsIngest.Api.Sources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Database
builder.Services.AddDbContext<TransactionsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TransactionsDb")));

// Import settings, validated at startup
builder.Services.AddOptions<ImportOptions>()
    .Bind(builder.Configuration.GetSection(ImportOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Source systems (mocked with JSON snapshot files)
builder.Services.AddOptions<SourceOptions>()
    .Bind(builder.Configuration.GetSection(SourceOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

var sourceOptions = builder.Configuration.GetSection(SourceOptions.SectionName).Get<SourceOptions>()
                    ?? new SourceOptions();
var sampleDataPath = Path.Combine(builder.Environment.ContentRootPath, sourceOptions.DataDirectory);

foreach (var system in sourceOptions.Systems)
{
    builder.Services.AddSingleton<ITransactionSource>(sp => new JsonFileTransactionSource(
        system.Name,
        Path.Combine(sampleDataPath, system.FileName),
        sp.GetRequiredService<ILogger<JsonFileTransactionSource>>()));
}

var app = builder.Build();

// Create or upgrade the SQLite database, then seed mock source files if missing.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    db.Database.Migrate();

    SampleDataSeeder.EnsureSampleData(sampleDataPath, sourceOptions.Systems, DateTime.UtcNow, app.Logger);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();