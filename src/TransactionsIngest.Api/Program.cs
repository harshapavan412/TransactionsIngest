using Microsoft.EntityFrameworkCore;
using TransactionsIngest.Api.Data;
using TransactionsIngest.Api.Import;

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

var app = builder.Build();

// Create or upgrade the SQLite database on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TransactionsDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();