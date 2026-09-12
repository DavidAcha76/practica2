using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BankApi.Shared;
using Microsoft.VisualBasic.FileIO;

namespace Practica.Bootstrap;

public sealed record CsvAccount(int BankId, string RecordId, PlainAccountInput Account);

public static class CsvAccounts
{
    public static List<CsvAccount> Read(string path, string? rejectionReport = null)
    {
        using var parser = new TextFieldParser(path, Encoding.UTF8) { HasFieldsEnclosedInQuotes = true, TrimWhiteSpace = true };
        parser.SetDelimiters(",");
        var headers = parser.ReadFields() ?? throw new InvalidDataException("El CSV esta vacio.");
        if (headers.Length == 1 && headers[0].StartsWith("Actualizado", StringComparison.OrdinalIgnoreCase))
            headers = parser.ReadFields() ?? throw new InvalidDataException("Falta la cabecera del CSV.");
        string[] required = ["Nro", "Identificacion", "Nombres", "Apellidos", "NroCuenta", "IdBanco", "Saldo"];
        var columns = headers.Select((name, index) => (Name: name.Trim('\uFEFF', ' '), Index: index)).ToDictionary(x => x.Name, x => x.Index, StringComparer.OrdinalIgnoreCase);
        if (required.Any(name => !columns.ContainsKey(name))) throw new InvalidDataException("Cabecera CSV requerida: " + string.Join(",", required));
        var rows = new Dictionary<string, CsvAccount>();
        var rejected = new List<string>();
        while (!parser.EndOfData)
        {
            var line = parser.LineNumber;
            var values = parser.ReadFields()!;
            if (values.Length != headers.Length) throw new InvalidDataException($"Cantidad de columnas incorrecta en la linea {line}.");
            string Field(string name) => values[columns[name]];
            if (!int.TryParse(Field("IdBanco"), out var bankId))
                throw new InvalidDataException($"IdBanco no numerico en linea {line}.");
            if (bankId is < 1 or > 14)
            {
                rejected.Add($"{line},{bankId},Banco no configurado (solo 1..14)");
                continue;
            }
            if (!decimal.TryParse(Field("Saldo"), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var balance))
                throw new InvalidDataException($"Saldo invalido en linea {line}.");
            var accountNumber = Field("NroCuenta");
            if (string.IsNullOrWhiteSpace(accountNumber)) throw new InvalidDataException($"NroCuenta vacio en linea {line}.");
            var account = new PlainAccountInput
            {
                CuentaId = accountNumber, NroCuenta = accountNumber, Identificacion = Field("Identificacion"),
                Nombres = Field("Nombres"), Apellidos = Field("Apellidos"), SaldoUSD = balance
            };
            var recordId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"csv-v1|{bankId}|{accountNumber}"))).ToLowerInvariant();
            var row = new CsvAccount(bankId, recordId, account);
            if (rows.TryGetValue(recordId, out var previous) && JsonSerializer.Serialize(previous.Account) != JsonSerializer.Serialize(account))
                throw new InvalidDataException($"Cuenta repetida con datos distintos en linea {line}, banco {bankId}.");
            rows[recordId] = row;
        }
        if (rows.Count == 0) throw new InvalidDataException("El CSV no contiene cuentas.");
        if (rejectionReport is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(rejectionReport))!);
            File.WriteAllLines(rejectionReport, new[] { "Linea,IdBanco,Motivo" }.Concat(rejected), Encoding.UTF8);
        }
        if (rejected.Count > 0) Console.WriteLine($"CSV: {rejected.Count} filas excluidas por banco desconocido. El archivo original se conserva." + (rejectionReport is null ? "" : $" Reporte: {rejectionReport}"));
        return rows.Values.ToList();
    }
}
