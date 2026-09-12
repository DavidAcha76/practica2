using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Asfi.Main.Api.Configuration;
using Asfi.Main.Api.Models;
using Asfi.Shared;
using Microsoft.Extensions.Options;

namespace Asfi.Main.Api.Services;

public sealed class RunCoordinator(
    QuoteClient quoteClient,
    BankApiClient bankClient,
    WorkerClient workerClient,
    AsfiRepository repository,
    AuditLog audit,
    CryptoProcessor localProcessor,
    RunStateStore states,
    IOptions<AsfiOptions> asfiOptions,
    IOptions<List<BankOptions>> banksOptions,
    IOptions<List<WorkerNodeOptions>> workerOptions,
    ILogger<RunCoordinator> logger)
{
    private readonly AsfiOptions _asfi = asfiOptions.Value;
    private readonly List<BankOptions> _banks = banksOptions.Value;
    private readonly List<WorkerNodeOptions> _workers = workerOptions.Value;

    public async Task ExecuteAsync(Guid runId, CancellationToken ct)
    {
        var run = states.Get(runId) ?? throw new InvalidOperationException("Run no encontrado.");
        var totalSw = Stopwatch.StartNew();
        try
        {
            run.Status = "ObteniendoCotizacion"; await repository.UpdateRunAsync(run, ct);
            var quote = await quoteClient.GetAsync(ct);
            run.ExchangeRate = quote.Rate;

            run.Status = "LeyendoBancos"; await repository.UpdateRunAsync(run, ct);
            var enabledBanks = _banks.Where(x => x.Enabled).ToList();
            var bankLists = new ConcurrentBag<List<EncryptedWorkItem>>();
            await Parallel.ForEachAsync(enabledBanks, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _asfi.BankFetchMaxParallelism), CancellationToken = ct }, async (bank, token) =>
            {
                bankLists.Add(await bankClient.FetchAsync(bank, token));
            });
            var all = bankLists.SelectMany(x => x).ToList();
            run.TotalRecords = all.Count;
            if (all.Count == 0) throw new InvalidOperationException("Las APIs bancarias respondieron, pero no se obtuvo ningún registro cifrado.");

            run.Status = "Procesando"; await repository.UpdateRunAsync(run, ct);
            var batchSize = Math.Clamp(_asfi.BatchSize, 50, 10000);
            var jobs = Channel.CreateBounded<WorkBatchRequest>(new BoundedChannelOptions(Math.Max(4, _workers.Count * 2 + 2)) { FullMode = BoundedChannelFullMode.Wait, SingleWriter = true });
            var results = Channel.CreateBounded<WorkBatchResponse>(new BoundedChannelOptions(Math.Max(4, _workers.Count * 2 + 2)) { FullMode = BoundedChannelFullMode.Wait });

            using var pipeline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            async Task Guard(Func<Task> action)
            {
                try { await action(); }
                catch { await pipeline.CancelAsync(); throw; }
            }
            var writerTask = Guard(() => PersistLoop(run, results.Reader, pipeline.Token));
            var consumers = new List<Task>();
            if (_asfi.ProcessLocally) consumers.Add(Guard(() => LocalConsumer(jobs.Reader, results.Writer, pipeline.Token)));
            foreach (var worker in _workers.Where(x => x.Enabled)) consumers.Add(Guard(() => RemoteConsumer(worker, jobs.Reader, results.Writer, pipeline.Token)));
            try
            {
                if (consumers.Count == 0) throw new InvalidOperationException("No hay procesador local ni workers habilitados.");
                var batchId = 0;
                for (var i = 0; i < all.Count; i += batchSize)
                {
                    var batch = new WorkBatchRequest { RunId = runId, BatchId = ++batchId, ExchangeRate = quote.Rate, Items = all.GetRange(i, Math.Min(batchSize, all.Count - i)) };
                    await jobs.Writer.WriteAsync(batch, pipeline.Token);
                }
                jobs.Writer.Complete();
                await Task.WhenAll(consumers);
                results.Writer.Complete();
                await writerTask;
            }
            catch
            {
                await pipeline.CancelAsync();
                jobs.Writer.TryComplete(); results.Writer.TryComplete();
                try { await Task.WhenAll(consumers.Append(writerTask)); } catch { }
                // Conservar el error original de persistencia/procesamiento, no solo la cancelacion.
                var failure = consumers.Append(writerTask).Select(t => t.Exception?.GetBaseException()).FirstOrDefault(e => e is not null && e is not OperationCanceledException);
                if (failure is not null) throw new InvalidOperationException("Fallo el procesamiento de lotes ASFI.", failure);
                throw;
            }

            if (run.ProcessedRecords != run.TotalRecords) throw new InvalidDataException("ASFI no proceso todos los registros recibidos.");

            run.Status = "Consolidando"; await repository.UpdateRunAsync(run, ct);
            await repository.ConsolidateAsync(runId, ct);
            totalSw.Stop();
            run.FinishedAtUtc = DateTime.UtcNow;
            run.DurationSeconds = totalSw.Elapsed.TotalSeconds;
            run.RecordsPerSecond = totalSw.Elapsed.TotalSeconds > 0 ? run.ProcessedRecords / totalSw.Elapsed.TotalSeconds : null;
            run.Status = run.FailedRecords == 0 ? "Completado" : "CompletadoConErrores";
            await repository.UpdateRunAsync(run, ct);
        }
        catch (Exception ex)
        {
            totalSw.Stop();
            run.Status = "Fallido"; run.Error = ex.GetBaseException().Message; run.FinishedAtUtc = DateTime.UtcNow; run.DurationSeconds = totalSw.Elapsed.TotalSeconds;
            try { await repository.UpdateRunAsync(run, CancellationToken.None); } catch { }
            logger.LogError(ex, "Run {RunId} falló", runId);
        }
    }

    private async Task LocalConsumer(ChannelReader<WorkBatchRequest> reader, ChannelWriter<WorkBatchResponse> output, CancellationToken ct)
    {
        await foreach (var batch in reader.ReadAllAsync(ct))
            await output.WriteAsync(await localProcessor.ProcessBatchAsync(batch, _asfi.LocalMaxParallelism, _asfi.NodeName, ct), ct);
    }

    private async Task RemoteConsumer(WorkerNodeOptions worker, ChannelReader<WorkBatchRequest> reader, ChannelWriter<WorkBatchResponse> output, CancellationToken ct)
    {
        var available = true;
        await foreach (var batch in reader.ReadAllAsync(ct))
        {
            if (available)
            {
                try { await output.WriteAsync(await workerClient.ProcessAsync(worker, batch, ct), ct); continue; }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    available = false;
                    logger.LogWarning(ex, "Worker {Worker} no disponible; fallback local para el resto de esta corrida.", worker.Name);
                }
            }
            await output.WriteAsync(await localProcessor.ProcessBatchAsync(batch, _asfi.LocalMaxParallelism, _asfi.NodeName + "-FALLBACK", ct), ct);
        }
    }

    private async Task PersistLoop(RunStatusDto run, ChannelReader<WorkBatchResponse> reader, CancellationToken ct)
    {
        await foreach (var response in reader.ReadAllAsync(ct))
        {
            await repository.BulkInsertAsync(response.Results, ct);
            await audit.AppendAsync(run.RunId, response.Results, ct);
            var ok = response.Results.Count(x => x.Success); var fail = response.Results.Count - ok;
            run.ProcessedRecords += response.Results.Count; run.SuccessRecords += ok; run.FailedRecords += fail;
            lock (run.Nodes)
            {
                if (!run.Nodes.TryGetValue(response.WorkerName, out var stat)) run.Nodes[response.WorkerName] = stat = new NodeStatDto();
                stat.Batches++; stat.Records += response.Results.Count; stat.ProcessingMs += response.DurationMs;
            }
            await repository.UpdateRunAsync(run, ct);
        }
    }
}
