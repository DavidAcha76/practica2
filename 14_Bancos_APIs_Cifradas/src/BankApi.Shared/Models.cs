
using System.Text.Json.Serialization;

namespace BankApi.Shared;

public sealed class PlainAccountInput
{
    public string CuentaId { get; set; } = "";
    public string Identificacion { get; set; } = "";
    public string Nombres { get; set; } = "";
    public string Apellidos { get; set; } = "";
    public string NroCuenta { get; set; } = "";
    public decimal SaldoUSD { get; set; }
}

public sealed class CryptoEnvelope
{
    public string CipherText { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class EncryptedAccountRecord
{
    public string RecordId { get; set; } = Guid.NewGuid().ToString("N");
    public int BancoId { get; set; }
    public string Algoritmo { get; set; } = "";
    public string CipherText { get; set; } = "";
    public Dictionary<string, string> Metadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class BankSettings
{
    public int BancoId { get; set; }
    public string Nombre { get; set; } = "";
    public string Algorithm { get; set; } = "";
    public string DatabaseEngine { get; set; } = "";
    public string ConnectionString { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    public string DatabaseUser { get; set; } = "";
    public string DatabasePassword { get; set; } = "";
}
