// ============================================================
// Neo4j — Banco 14 (Banco de la Nación Argentina - ChaCha20)
//
// Ejecutar en Neo4j Browser: http://localhost:7474
// O con:
//   cypher-shell -u neo4j -p 12345678 -f init_banco_neo4j.cypher
// ============================================================

// Constraints únicos
CREATE CONSTRAINT cuenta_nro_cuenta IF NOT EXISTS
FOR (c:Cuenta) REQUIRE c.nro_cuenta IS UNIQUE;

CREATE CONSTRAINT cuenta_nro IF NOT EXISTS
FOR (c:Cuenta) REQUIRE c.nro IS UNIQUE;

CREATE CONSTRAINT titular_nro IF NOT EXISTS
FOR (t:Titular) REQUIRE t.nro IS UNIQUE;

// Índices
CREATE INDEX cuenta_idx IF NOT EXISTS
FOR (c:Cuenta) ON (c.nro_cuenta);

CREATE INDEX titular_idx IF NOT EXISTS
FOR (t:Titular) ON (t.nro);

// Nodo del banco
MERGE (:Banco {
    id_banco:  14,
    nombre:    "Banco de la Nación Argentina",
    algoritmo: "ChaCha20",
    motor_bd:  "Neo4j"
});

RETURN "Neo4j: constraints e indices creados OK" AS status;
