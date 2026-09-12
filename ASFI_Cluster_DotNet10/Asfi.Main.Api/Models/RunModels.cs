namespace Asfi.Main.Api.Models;

public sealed class RunStatusDto
{
    public Guid RunId { get; set; }
    public string Status { get; set; } = "Pending";
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public decimal? ExchangeRate { get; set; }
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SuccessRecords { get; set; }
    public int FailedRecords { get; set; }
    public double? DurationSeconds { get; set; }
    public double? RecordsPerSecond { get; set; }
    public string? Error { get; set; }
    public Dictionary<string, NodeStatDto> Nodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class NodeStatDto
{
    public int Batches { get; set; }
    public int Records { get; set; }
    public long ProcessingMs { get; set; }
}

public sealed record CreateRunResponse(Guid RunId, string Status, string StatusUrl);

public sealed class DependencyStatusDto
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool Ok { get; set; }
    public int? HttpStatus { get; set; }
    public long Ms { get; set; }
    public string? Detail { get; set; }
}
