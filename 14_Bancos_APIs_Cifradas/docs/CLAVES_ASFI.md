# Claves que necesitaría ASFI para descifrar (entorno académico)

> Este archivo existe para que el proyecto sea demostrable. No representa una política segura de gestión de llaves.

## Cifrados clásicos

- Banco Unión / César: desplazamiento `3`.
- Banco Mercantil / Atbash: no usa clave.
- BNB / Vigenère: palabra clave `BOLIVIA`.
- BCP / Playfair: palabra clave `BANCOBCP` sobre el alfabeto A..P.
- BISA / Hill: matriz `[[3,3],[2,5]]` módulo 16. Inversa `[[13,5],[14,11]]`.

## Simétricos

- Banco Ganadero / DES: `dTszaR3d1yQ=` (Base64)
- Banco Económico / 3DES: `9bN2eU4F/hoECd6yqCInPKzRTO6nba/w` (Base64)
- Banco Prodem / Blowfish: `b0aBRxfawVIoEE8r+sE+UqAPGVjQoAbGv3x70h6ICF4=` (Base64)
- Banco Solidario / Twofish: `3qsHfxueYlqmJmIjLeeyc2WzZQMGCWBbMbaXuDHiZZE=` (Base64)
- Banco Fortaleza / AES-256-GCM: `eWIIlDcshoFIFHzJqPhviEauznHUz2EJxhSlePXGEv0=` (Base64)
- Banco Nación Argentina / ChaCha20-Poly1305: `dP5TYN35BTb8e8076Q+rpcJduPF8wDm3oy8iE3rrtA0=` (Base64)

## Asimétricos / híbridos

- Banco FIE / RSA: `../asfi-keys/BancoFIE_RSA_PRIVATE.pem`
- Banco PYME / ElGamal: `../asfi-keys/BancoPYME_ElGamal_PRIVATE.json`
- BDP / ECC: `../asfi-keys/BDP_ECC_PRIVATE.pem`

Los bancos solo incluyen la parte pública para RSA/ECC. Para ElGamal, la API contiene `p`, `g` e `y`, pero no `x`.
