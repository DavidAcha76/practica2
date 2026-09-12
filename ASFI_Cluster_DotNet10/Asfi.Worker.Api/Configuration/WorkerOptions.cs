namespace Asfi.Worker.Api.Configuration;

public sealed class WorkerOptions
{
    public string NodeName { get; set; } = "ASFI-WORKER";
    public int MaxParallelism { get; set; } = 0;
    public string KeyFile { get; set; } = "../config/asfi-keys.json";
    public string ApiKey { get; set; } = "ASFI-CLUSTER-DEMO-2026";
}
