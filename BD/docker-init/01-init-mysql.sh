#!/bin/bash
set -e

echo "=== Inicializando bases de datos MySQL para Bancos 6 a 9 ==="

for db in banco_ganadero banco_economico banco_prodem banco_solidario; do
    echo ">> Creando base de datos: $db"
    mysql -u root -p"$MYSQL_ROOT_PASSWORD" -e "CREATE DATABASE IF NOT EXISTS $db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
    echo ">> Aplicando esquema en: $db"
    mysql -u root -p"$MYSQL_ROOT_PASSWORD" "$db" < /docker-entrypoint-initdb.d/init_bancos_mysql.sql
done

echo "=== MySQL: Bancos 6 a 9 inicializados exitosamente ==="
