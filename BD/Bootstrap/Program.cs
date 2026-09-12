using Practica.Bootstrap;

try
{
    if (args.Length < 2) throw new ArgumentException("Uso: Bootstrap <configure|init|check|import|verify|verify-run|clear|clear-plan> <raiz> [archivo.csv]; verify-run <raiz> <runId> [archivo.csv]");
    var workspace = new Workspace(Path.GetFullPath(args[1]));
    switch (args[0])
    {
        case "configure": workspace.Configure(); break;
        case "init": await workspace.InitializeAsync(); break;
        case "check": await workspace.CheckDatabasesAsync(); break;
        case "clear": await workspace.ClearAsync(); break;
        case "clear-plan": workspace.ShowClearPlan(); break;
        case "import": await workspace.ImportAsync(args.Length > 2 ? Path.GetFullPath(args[2]) : Path.Combine(workspace.Root, "BD/dataset.csv")); break;
        case "verify-run":
            if (args.Length < 3) throw new ArgumentException("Falta el runId a verificar.");
            await workspace.VerifyRunAsync(Guid.Parse(args[2]), args.Length > 3 ? Path.GetFullPath(args[3]) : null);
            break;
        case "verify":
            await workspace.VerifyCryptoAsync();
            var rows = CsvAccounts.Read(args.Length > 2 ? args[2] : Path.Combine(workspace.Root, "BD/dataset.csv"));
            Console.WriteLine($"CSV valido: {rows.Count} cuentas; {rows.Select(x => x.BankId).Distinct().Count()} bancos.");
            break;
        default: throw new ArgumentException("Comando no reconocido.");
    }
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERROR: {ex.Message}");
    return 1;
}
