using System.Collections.Concurrent;
using Asfi.Main.Api.Models;

namespace Asfi.Main.Api.Services;

public sealed class RunStateStore
{
    private readonly ConcurrentDictionary<Guid, RunStatusDto> _runs = new();

    public RunStatusDto Create(Guid id)
    {
        var run = new RunStatusDto { RunId = id, Status = "Pending", StartedAtUtc = DateTime.UtcNow };
        _runs[id] = run;
        return run;
    }

    public RunStatusDto? Get(Guid id) => _runs.TryGetValue(id, out var run) ? run : null;
    public IReadOnlyCollection<RunStatusDto> All() => _runs.Values.OrderByDescending(x => x.StartedAtUtc).ToArray();
}
