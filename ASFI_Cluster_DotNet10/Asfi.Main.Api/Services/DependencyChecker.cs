using System.Diagnostics;
using Asfi.Main.Api.Configuration;
using Asfi.Main.Api.Models;
using Microsoft.Extensions.Options;

namespace Asfi.Main.Api.Services;

public sealed class DependencyChecker(IHttpClientFactory factory, IOptions<BcbOptions> bcb, IOptions<List<BankOptions>> banks, IOptions<List<WorkerNodeOptions>> workers)
{
    public async Task<List<DependencyStatusDto>> CheckAsync(CancellationToken ct)
    {
        var targets = new List<(string Name,string Type,string Url)>
        {
            ("BCB", "BCB", BankApiClient.Combine(bcb.Value.BaseUrl,bcb.Value.Endpoint))
        };
        targets.AddRange(banks.Value.Where(x=>x.Enabled).Select(x=>(x.Name,"Banco",BankApiClient.Combine(x.BaseUrl,x.InfoEndpoint))));
        targets.AddRange(workers.Value.Where(x=>x.Enabled).Select(x=>(x.Name,"Worker",BankApiClient.Combine(x.BaseUrl,"/api/worker/health"))));
        var client = factory.CreateClient("health");
        var tasks = targets.Select(async t =>
        {
            var sw=Stopwatch.StartNew();
            try { using var res=await client.GetAsync(t.Url,ct); sw.Stop(); return new DependencyStatusDto{Name=t.Name,Type=t.Type,Url=t.Url,Ok=res.IsSuccessStatusCode,HttpStatus=(int)res.StatusCode,Ms=sw.ElapsedMilliseconds,Detail=res.ReasonPhrase}; }
            catch(Exception ex){sw.Stop();return new DependencyStatusDto{Name=t.Name,Type=t.Type,Url=t.Url,Ok=false,Ms=sw.ElapsedMilliseconds,Detail=ex.GetBaseException().Message};}
        });
        return (await Task.WhenAll(tasks)).ToList();
    }
}
