// ============================================================
// MongoDB — Bancos 12-13
// Banco 12: banco_pyme           (ElGamal)
// Banco 13: banco_des_productivo (ECC)
//
// Ejecutar:
//   mongosh < init_bancos_mongodb.js
// ============================================================

// ── Banco 12: PYME ───────────────────────────────────────────
db = db.getSiblingDB('banco_pyme');
db.cuentas.drop();
db.createCollection('cuentas');

db.cuentas.createIndex({ nro: 1 },         { unique: true });
db.cuentas.createIndex({ nro_cuenta: 1 },  { unique: true, name: "idx_nro_cuenta" });
db.cuentas.createIndex({ convertido_at: 1 });

print('banco_pyme: coleccion cuentas creada OK');

// ── Banco 13: Desarrollo Productivo ─────────────────────────
db = db.getSiblingDB('banco_des_productivo');
db.cuentas.drop();
db.createCollection('cuentas');

db.cuentas.createIndex({ nro: 1 },         { unique: true });
db.cuentas.createIndex({ nro_cuenta: 1 },  { unique: true, name: "idx_nro_cuenta" });
db.cuentas.createIndex({ convertido_at: 1 });

print('banco_des_productivo: coleccion cuentas creada OK');
