# Configuración de bases de datos

Las credenciales están en cada `src/<Banco>.Api/appsettings.json`.

Valores académicos por defecto:

- PostgreSQL: usuario `postgres`, contraseña `postgres`, puerto 5432.
- MySQL: usuario `root`, contraseña `root`, puerto 3306.
- SQL Server: `localhost\SQLEXPRESS` con autenticación integrada de Windows.
- MongoDB: `mongodb://localhost:27017`.
- Neo4j: `bolt://localhost:7687`, usuario `neo4j`, contraseña `neo4j12345`.

Cambie únicamente esos valores para adaptar el proyecto a su PC. No tiene que modificar el código de cifrado.

Los repositorios intentan crear la tabla/índice/constraint al iniciar, pero la base o motor debe existir y estar accesible. Los scripts de `databases/` preparan la estructura.
