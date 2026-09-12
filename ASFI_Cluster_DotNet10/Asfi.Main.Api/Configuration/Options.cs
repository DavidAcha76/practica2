namespace Asfi.Main.Api.Configuration;

public sealed class AsfiOptions
{
    public string NodeName { get; set; } = "ASFI-MAIN-I7-12700H";
    public int BatchSize { get; set; } = 1000;
    public int LocalMaxParallelism { get; set; } = 0;
    public int BankFetchMaxParallelism { get; set; } = 14;
    public bool ProcessLocally { get; set; } = true;
    public string KeyFile { get; set; } = "../config/asfi-keys.json";
    public string AuditDirectory { get; set; } = "audit";
    public string WorkerApiKey { get; set; } = "";
}

public sealed class BcbOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5050";
    public string Endpoint { get; set; } = "/api/cotizacion";
}

public sealed class BankOptions
{
    public int BankId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Algorithm { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string AccountsEndpoint { get; set; } = "/api/cuentas";
    public string InfoEndpoint { get; set; } = "/api/banco/info";
    public bool Enabled { get; set; } = true;
    public string? UpdateBatchEndpoint { get; set; }
    public string? VerifyBatchEndpoint { get; set; }
}

public sealed class WorkerNodeOptions
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}
