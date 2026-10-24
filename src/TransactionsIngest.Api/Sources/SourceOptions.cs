using System.ComponentModel.DataAnnotations;

namespace TransactionsIngest.Api.Sources;

/// <summary>
/// Bound from the "Sources" section of appsettings.json.
/// </summary>
public class SourceOptions
{
    public const string SectionName = "Sources";

    /// <summary>Folder (relative to the API project) holding the mock snapshot files.</summary>
    [Required]
    public string DataDirectory { get; set; } = "sample-data";

    [MinLength(1)]
    public List<SourceSystemOptions> Systems { get; set; } = new();
}

public class SourceSystemOptions
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string FileName { get; set; } = string.Empty;
}