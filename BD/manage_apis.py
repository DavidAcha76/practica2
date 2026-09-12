"""Compatibilidad: los BAT de la raiz son el punto de entrada recomendado."""
import argparse
from compat import stack

parser = argparse.ArgumentParser()
parser.add_argument("command", choices=["start", "stop", "restart", "status"])
args = parser.parse_args()
if args.command == "restart":
    stack("Stop")
    stack("Start")
else:
    stack({"start": "Start", "stop": "Stop", "status": "Status"}[args.command])
