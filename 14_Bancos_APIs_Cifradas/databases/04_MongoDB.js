// Ejecutar con mongosh: mongosh < 04_MongoDB.js
for (const dbName of ["banco_bcp", "banco_solidario", "banco_nacion_argentina"]) {
  const b = db.getSiblingDB(dbName);
  if (!b.getCollectionNames().includes("encrypted_accounts")) b.createCollection("encrypted_accounts");
  b.encrypted_accounts.createIndex({ recordId: 1 }, { unique: true });
}
