using System.Text.Json;

namespace TransactionsIngest.Api.Sources;

/// <summary>
/// Creates the mock snapshot files on first run, with timestamps relative to "now",
/// so the 24-hour revocation window and finalization can both be demonstrated.
/// Existing files are never overwritten, so edits made for testing are kept.
/// </summary>
public static class SampleDataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static void EnsureSampleData(
        string dataDirectory,
        IEnumerable<SourceSystemOptions> systems,
        DateTime utcNow,
        ILogger logger)
    {
        Directory.CreateDirectory(dataDirectory);

        foreach (var system in systems)
        {
            var path = Path.Combine(dataDirectory, system.FileName);
            if (File.Exists(path))
            {
                continue;
            }

            var snapshot = BuildSnapshot(system.Name, utcNow);
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions));
            logger.LogInformation("Seeded {Count} sample transactions for {SourceSystem} at {Path}",
                snapshot.Count, system.Name, path);
        }
    }

    private static List<SourceTransaction> BuildSnapshot(string source, DateTime utcNow)
    {
        // Whole seconds keep the file readable and the values stable.
        var now = new DateTime(utcNow.Ticks - utcNow.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

        return source switch
        {
            "Store Uptown" =>
            [
                new(source, 100001, "***1111", "UPTOWN", "Wireless Mouse", 24.99m, now.AddHours(-2)),
                new(source, 100002, "***2222", "UPTOWN", "HDMI Cable", 12.50m, now.AddHours(-5)),
                new(source, 100003, "", "UPTOWN", "Gift Card", 50.00m, now.AddHours(-8)),
                new(source, 100004, "***3333", "UPTOWN", "Laptop Stand", 39.99m, now.AddHours(-30)),
                new(source, 100005, "***1111", "UPTOWN", "Webcam", 59.00m, now.AddHours(-50)),
            ],
            "Store Downtown" =>
            [
                new(source, 100001, "***0002", "DOWNTOWN", "Mechanical Keyboard", 89.99m, now.AddHours(-1)),
                new(source, 100002, "***0005", "DOWNTOWN", "Monitor Arm", 74.25m, now.AddHours(-6)),
                new(source, 100003, "***0007", "DOWNTOWN", "Desk Lamp", 32.00m, now.AddHours(-12)),
                new(source, 100004, "", "DOWNTOWN", "Phone Charger", 19.99m, now.AddHours(-36)),
            ],
            "Online" =>
            [
                new(source, 100001, "***4444", "ONLINE", "USB-C Dock", 129.95m, now.AddHours(-3)),
                new(source, 100002, "***5555", "ONLINE", "Bluetooth Speaker", 149.00m, now.AddHours(-4)),
                new(source, 100003, "***6666", "ONLINE", "Ergonomic Chair", 249.99m, now.AddHours(-20)),
                // Deliberately invalid: product name is longer than the 20-character limit.
                new(source, 100004, "***4444", "ONLINE", "USB-C Docking Station", 129.95m, now.AddHours(-7)),
                new(source, 100005, "***7777", "ONLINE", "External SSD 1TB", 99.99m, now.AddHours(-40)),
            ],
            _ => []
        };
    }
}