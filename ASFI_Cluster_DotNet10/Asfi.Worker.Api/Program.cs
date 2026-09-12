using Asfi.Shared;
using Asfi.Worker.Api.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://0.0.0.0:5201");
builder.Services.Configure<WorkerOptions>(builder.Configuration.GetSection("Worker"));
var keyPathCfg = builder.Configuration["Worker:KeyFile"] ?? "../config/asfi-keys.json";
var keyPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, keyPathCfg));
builder.Services.AddSingleton(CryptoKeyRing.Load(keyPath));
builder.Services.AddSingleton<CryptoProcessor>();
var app = builder.Build();

bool Authorized(HttpContext ctx, WorkerOptions options) => string.IsNullOrWhiteSpace(options.ApiKey) || ctx.Request.Headers.TryGetValue("X-Worker-Key", out var v) && string.Equals(v.ToString(), options.ApiKey, StringComparison.Ordinal);

app.MapGet("/", (IOptions<WorkerOptions> opt) => Results.Ok(new { service="ASFI Worker API", node=opt.Value.NodeName, processorCount=Environment.ProcessorCount, endpoints=new[]{"GET /api/worker/health","POST /api/worker/process"} }));
app.MapGet("/api/worker/health", (IOptions<WorkerOptions> opt) => Results.Ok(new WorkerHealthResponse { NodeName=opt.Value.NodeName, MachineName=Environment.MachineName, ProcessorCount=Environment.ProcessorCount, MaxParallelism=opt.Value.MaxParallelism<=0?Environment.ProcessorCount:opt.Value.MaxParallelism, UtcNow=DateTime.UtcNow }));
app.MapPost("/api/worker/process", async (WorkBatchRequest batch, HttpContext ctx, CryptoProcessor processor, IOptions<WorkerOptions> opt) =>
{
    if (!Authorized(ctx,opt.Value)) return Results.Unauthorized();
    if (batch.Items.Count == 0) return Results.BadRequest(new { error="El lote no contiene registros." });
    var result=await processor.ProcessBatchAsync(batch,opt.Value.MaxParallelism,opt.Value.NodeName,ctx.RequestAborted);
    return Results.Ok(result);
});
app.Run();
