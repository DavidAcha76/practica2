using Asfi.Main.Api.Configuration;
using Asfi.Main.Api.Models;
using Asfi.Main.Api.Services;
using Asfi.Shared;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://localhost:5000");

builder.Services.Configure<AsfiOptions>(builder.Configuration.GetSection("Asfi"));
builder.Services.Configure<BcbOptions>(builder.Configuration.GetSection("BCB"));
builder.Services.Configure<List<BankOptions>>(builder.Configuration.GetSection("Banks"));
builder.Services.Configure<List<WorkerNodeOptions>>(builder.Configuration.GetSection("Workers"));

builder.Services.AddHttpClient("banks", c => c.Timeout = TimeSpan.FromMinutes(3)).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { MaxConnectionsPerServer = 64, PooledConnectionLifetime = TimeSpan.FromMinutes(10) });
builder.Services.AddHttpClient("workers", c => c.Timeout = TimeSpan.FromMinutes(5)).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { MaxConnectionsPerServer = 8, PooledConnectionLifetime = TimeSpan.FromMinutes(10) });
builder.Services.AddHttpClient("health", c => c.Timeout = TimeSpan.FromSeconds(5));
builder.Services.AddHttpClient<QuoteClient>(c => c.Timeout = TimeSpan.FromSeconds(15));

var keyPathCfg = builder.Configuration["Asfi:KeyFile"] ?? "../config/asfi-keys.json";
var keyPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, keyPathCfg));
builder.Services.AddSingleton(CryptoKeyRing.Load(keyPath));
builder.Services.AddSingleton<CryptoProcessor>();
builder.Services.AddSingleton<RunStateStore>();
builder.Services.AddSingleton<RunQueue>();
builder.Services.AddSingleton<AuditLog>();
builder.Services.AddSingleton<BankApiClient>();
builder.Services.AddSingleton<WorkerClient>();
builder.Services.AddSingleton<DependencyChecker>();
builder.Services.AddSingleton<AsfiRepository>();
builder.Services.AddScoped<RunCoordinator>();
builder.Services.AddHostedService<RunBackgroundService>();

var app = builder.Build();
if (builder.Configuration.GetValue<bool>("Asfi:InitializeDatabaseOnStartup"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AsfiRepository>().InitializeAsync(CancellationToken.None);
}
else
{
    app.Logger.LogInformation("Inicialización de SQL Server omitida. Se puede comprobar la conectividad con /api/asfi/dependencies; el procesamiento requiere la base de datos.");
}

app.MapGet("/", () => Results.Ok(new { service="ASFI Main API", framework="ASP.NET Core .NET 10", role="Coordinator + local worker + SQL Server", endpoints=new[]{"POST /api/asfi/runs","GET /api/asfi/runs","GET /api/asfi/runs/{id}","GET /api/asfi/runs/{id}/results","GET /api/asfi/dependencies"} }));

app.MapPost("/api/asfi/runs", async (RunStateStore states, RunQueue queue, AsfiRepository repo, HttpContext ctx) =>
{
    var id=Guid.NewGuid(); var run=states.Create(id); await repo.CreateRunAsync(run,ctx.RequestAborted); await queue.EnqueueAsync(id,ctx.RequestAborted);
    return Results.Accepted($"/api/asfi/runs/{id}", new CreateRunResponse(id,"Pending",$"/api/asfi/runs/{id}"));
});
app.MapGet("/api/asfi/runs", (RunStateStore states) => Results.Ok(states.All()));
app.MapGet("/api/asfi/runs/{id:guid}", (Guid id, RunStateStore states) => states.Get(id) is { } r ? Results.Ok(r) : Results.NotFound());
app.MapGet("/api/asfi/runs/{id:guid}/results", async (Guid id, int? take, AsfiRepository repo, HttpContext ctx) => Results.Ok(await repo.GetResultsAsync(id,take??100,ctx.RequestAborted)));
app.MapGet("/api/asfi/dependencies", async (DependencyChecker checker, HttpContext ctx) => Results.Ok(await checker.CheckAsync(ctx.RequestAborted)));
app.MapGet("/api/asfi/config-summary", (IConfiguration cfg) => Results.Ok(new
{
    node = cfg["Asfi:NodeName"],
    logicalProcessors = Environment.ProcessorCount,
    batchSize = cfg.GetValue<int>("Asfi:BatchSize"),
    initializeDatabaseOnStartup = cfg.GetValue<bool>("Asfi:InitializeDatabaseOnStartup"),
    bcb = BankApiClient.Combine(cfg["BCB:BaseUrl"]!, cfg["BCB:Endpoint"]!),
    banks = cfg.GetSection("Banks").GetChildren().Select(b => new
    {
        bankId = b.GetValue<int>("BankId"),
        baseUrl = b["BaseUrl"],
        infoUrl = BankApiClient.Combine(b["BaseUrl"]!, b["InfoEndpoint"]!),
        accountsUrl = BankApiClient.Combine(b["BaseUrl"]!, b["AccountsEndpoint"]!),
        enabled = b.GetValue<bool>("Enabled")
    }),
    workers = cfg.GetSection("Workers").GetChildren().Select(w => new
    {
        name = w["Name"], baseUrl = w["BaseUrl"], enabled = w.GetValue<bool>("Enabled")
    })
}));

app.Run();
