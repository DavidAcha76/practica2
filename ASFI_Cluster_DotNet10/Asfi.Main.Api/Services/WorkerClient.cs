using System.Net.Http.Json;
using Asfi.Main.Api.Configuration;
using Asfi.Shared;
using Microsoft.Extensions.Options;

namespace Asfi.Main.Api.Services;

public sealed class WorkerClient(IHttpClientFactory factory, IOptions<AsfiOptions> asfiOptions)
{
    private readonly AsfiOptions _asfi = asfiOptions.Value;

    public async Task<WorkBatchResponse> ProcessAsync(WorkerNodeOptions worker, WorkBatchRequest batch, CancellationToken ct)
    {
        var client = factory.CreateClient("workers");
        using var req = new HttpRequestMessage(HttpMethod.Post, BankApiClient.Combine(worker.BaseUrl, "/api/worker/process"))
        {
            Content = JsonContent.Create(batch)
        };
        if (!string.IsNullOrWhiteSpace(_asfi.WorkerApiKey)) req.Headers.TryAddWithoutValidation("X-Worker-Key", _asfi.WorkerApiKey);
        using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<WorkBatchResponse>(cancellationToken: ct)
               ?? throw new InvalidDataException($"Worker {worker.Name} devolvió respuesta vacía.");
    }
}
