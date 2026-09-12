using System.Threading.Channels;

namespace Asfi.Main.Api.Services;

public sealed class RunQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    public ValueTask EnqueueAsync(Guid runId, CancellationToken ct) => _channel.Writer.WriteAsync(runId, ct);
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

public sealed class RunBackgroundService(RunQueue queue, IServiceScopeFactory scopes, ILogger<RunBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<RunCoordinator>().ExecuteAsync(runId, stoppingToken);
            }
            catch (Exception ex) { logger.LogError(ex, "Error no controlado ejecutando run {RunId}", runId); }
        }
    }
}
