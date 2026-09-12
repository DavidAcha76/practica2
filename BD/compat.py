"""Entradas Python antiguas: delegan en el mismo inicializador que los BAT."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parent.parent
PROJECT = ROOT / "BD/Bootstrap/Bootstrap.csproj"
DLL = ROOT / "BD/Bootstrap/bin/Debug/net10.0/Bootstrap.dll"


def bootstrap(command, build=False):
    if build or not DLL.exists():
        subprocess.run(["dotnet", "build", str(PROJECT), "--nologo"], cwd=ROOT, check=True)
    subprocess.run(["dotnet", str(DLL), command, str(ROOT)], cwd=ROOT, check=True)


def stack(action, csv=""):
    args = ["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
            str(ROOT / "scripts/Run-Stack.ps1"), "-Action", action]
    if csv:
        args += ["-CsvPath", str(Path(csv).resolve())]
    subprocess.run(args, cwd=ROOT, check=True)
