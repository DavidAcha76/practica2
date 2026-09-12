
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BankApi.Shared;

public static class BankApiBootstrap
{
    public static async Task RunAsync(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var c = builder.Configuration;
        var settings = new BankSettings
        {
            BancoId=int.Parse(c["Bank:BancoId"] ?? "0"), Nombre=c["Bank:Nombre"] ?? "", Algorithm=c["Bank:Algorithm"] ?? "",
            DatabaseEngine=c["Database:Engine"] ?? "", ConnectionString=c["Database:ConnectionString"] ?? "", DatabaseName=c["Database:DatabaseName"] ?? "",
            DatabaseUser=c["Database:User"] ?? "", DatabasePassword=c["Database:Password"] ?? ""
        };

        builder.Services.AddSingleton(settings);
        builder.Services.AddSingleton<IAccountCipher>(_ => CipherFactory.Create(c));
        builder.Services.AddSingleton<IEncryptedAccountRepository>(_ => RepositoryFactory.Create(settings));

        var app = builder.Build();
        // Intencionalmente NO se usa CORS, autenticación, autorización, HSTS ni redirección HTTPS.
        if (c.GetValue<bool>("Database:InitializeOnStartup"))
        {
            var repo = app.Services.GetRequiredService<IEncryptedAccountRepository>();
            try { await repo.InitializeAsync(); }
            catch (Exception ex) { app.Logger.LogWarning(ex, "No se pudo inicializar la base. La API arrancará, pero los endpoints de datos fallarán hasta que el motor esté disponible."); }
        }
        else
        {
            app.Logger.LogInformation("Inicialización de base de datos omitida. /api/banco/info está disponible; los endpoints de cuentas requieren la base de datos.");
        }

        app.MapGet("/", () => Results.Ok(new { servicio="API bancaria académica", settings.BancoId, banco=settings.Nombre, algoritmo=settings.Algorithm, motor=settings.DatabaseEngine, endpoints=new[]{"GET /api/banco/info","GET /api/cuentas","GET /api/cuentas/{recordId}","POST /api/cuentas","POST /api/cuentas/seed-demo"} }));
        app.MapGet("/api/banco/info", () => Results.Ok(new { settings.BancoId, settings.Nombre, Algoritmo=settings.Algorithm, BaseDeDatos=settings.DatabaseEngine, SoloDatosCifrados=true }));
        app.MapGet("/api/cuentas", async (IEncryptedAccountRepository r) => Results.Ok(await r.GetAllAsync()));
        app.MapGet("/api/cuentas/{id}", async (string id, IEncryptedAccountRepository r) => { var x=await r.GetByIdAsync(id); return x is null?Results.NotFound():Results.Ok(x); });

        app.MapPost("/api/cuentas", async (PlainAccountInput input, IAccountCipher cipher, IEncryptedAccountRepository r) =>
        {
            var json=JsonSerializer.Serialize(input); var e=cipher.Encrypt(json);
            var record=new EncryptedAccountRecord{BancoId=settings.BancoId,Algoritmo=settings.Algorithm,CipherText=e.CipherText,Metadata=e.Metadata};
            await r.InsertAsync(record); return Results.Created($"/api/cuentas/{record.RecordId}",record);
        });

        app.MapPost("/api/cuentas/seed-demo", async (IAccountCipher cipher, IEncryptedAccountRepository r) =>
        {
            var demo = new[]
            {
                new PlainAccountInput{CuentaId=$"{settings.BancoId}-001",Identificacion="937132657",Nombres="Lucia",Apellidos="Garcia Vargas",NroCuenta=$"9999{settings.BancoId:00}1601158610",SaldoUSD=470754.2664m},
                new PlainAccountInput{CuentaId=$"{settings.BancoId}-002",Identificacion="2710659136",Nombres="Sofia Diego Juan",Apellidos="Lopez Mendoza",NroCuenta=$"9999{settings.BancoId:00}3843998200",SaldoUSD=41312.0372m},
                new PlainAccountInput{CuentaId=$"{settings.BancoId}-003",Identificacion="3358295803",Nombres="Sofia Diego Carlos",Apellidos="Mamani Mendoza",NroCuenta=$"9999{settings.BancoId:00}3442711440",SaldoUSD=418731.8101m}
            };
            var output=new List<EncryptedAccountRecord>();
            foreach(var input in demo){var e=cipher.Encrypt(JsonSerializer.Serialize(input));var rec=new EncryptedAccountRecord{BancoId=settings.BancoId,Algoritmo=settings.Algorithm,CipherText=e.CipherText,Metadata=e.Metadata};await r.InsertAsync(rec);output.Add(rec);} return Results.Ok(output);
        });

        await app.RunAsync();
    }
}
