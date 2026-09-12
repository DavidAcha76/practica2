"""Carga cifrada e idempotente; equivalente a CARGAR_CSV.bat."""
import argparse
from compat import stack

parser = argparse.ArgumentParser()
parser.add_argument("--csv", default="")
args = parser.parse_args()
stack("Import", args.csv)
