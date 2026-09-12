// Ejecutar en Neo4j Browser/Cypher Shell sobre la base configurada (por defecto neo4j).
// Banco BISA (bankId=5) y Banco Fortaleza (bankId=10) comparten el motor Neo4j local,
// pero están aislados lógicamente por bankId. En Neo4j Enterprise puede configurar DatabaseName distinto por API.
CREATE CONSTRAINT encrypted_account_unique IF NOT EXISTS
FOR (a:EncryptedAccount) REQUIRE (a.bankId, a.recordId) IS UNIQUE;
