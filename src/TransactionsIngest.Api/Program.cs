using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Import;
using TransactionsIngest.Api.Sources;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Database
builder.Services.AddDbContext<TransactionsDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TransactionsDb")));

// Import
builder.Services.AddOptions<ImportOptions>()
    .Bind(builder.Configuration.GetSection(ImportOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IImportService, ImportService>();

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

// CORS for the Angular dev server
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// Create or upgrade the SQLite database, then seed mock source files if missing.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    db.Database.Migrate();

    SampleDataSeeder.EnsureSampleData(sampleDataPath, sourceOptions.Systems, DateTime.UtcNow, app.Logger);
}

// Unhandled exceptions become a standard JSON error (RFC 7807 ProblemDetails), never a stack trace.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();