#!/bin/bash
set -e

echo "=== Inicializando bases de datos PostgreSQL para Bancos 1 a 5 ==="

for db in banco_union banco_mercantil banco_bnb banco_bcp banco_bisa; do
    echo ">> Creando base de datos: $db"
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<-EOSQL
        SELECT 'CREATE DATABASE $db'
        WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '$db')\gexec
EOSQL

    echo ">> Aplicando esquema en: $db"
    psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$db" -f /docker-entrypoint-initdb.d/init_bancos_postgresql.sql
done

echo "=== PostgreSQL: Bancos 1 a 5 inicializados exitosamente ==="
