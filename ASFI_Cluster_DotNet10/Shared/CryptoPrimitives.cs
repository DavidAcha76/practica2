using System.Globalization;
using System.Security.Cryptography;
using ChaCha20Poly1305 = System.Security.Cryptography.ChaCha20Poly1305;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.OpenSsl;

namespace Asfi.Shared;

public static class CryptoPrimitives
{
    private const string HexAlphabet = "ABCDEFGHIJKLMNOP";

    public static string NormalizeAlgorithm(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var s = value.Trim().ToUpperInvariant()
            .Replace("Á", "A").Replace("É", "E").Replace("Í", "I").Replace("Ó", "O").Replace("Ú", "U")
            .Replace("-", "").Replace("_", "").Replace(" ", "");
        return s switch
        {
            "CESAR" or "CAESAR" => "CAESAR",
            "VIGENERE" => "VIGENERE",
            "PLAYFAIR" => "PLAYFAIR",
            "HILL" => "HILL",
            "TRIPLEDES" or "3DES" or "TDES" => "3DES",
            "BLOWFISH" => "BLOWFISH",
            "TWOFISH" => "TWOFISH",
            "AES" or "AES256GCM" => "AES",
            "RSA" => "RSA",
            "ELGAMAL" => "ELGAMAL",
            "ECC" or "ECDH" => "ECC",
            "CHACHA20" or "CHACHA20POLY1305" => "CHACHA20",
            "ATBASH" => "ATBASH",
            "DES" => "DES",
            _ => s
        };
    }

    public static byte[] DecryptToPlainBytes(CryptoEnvelopeDto env, CryptoKeyEntry key)
    {
        var alg = NormalizeAlgorithm(env.Algorithm);
        return alg switch
        {
            "CAESAR" => DecodeAp(DecryptCaesar(ReadCipherText(env), key.IntValue ?? 3), env.PlainLength),
            "ATBASH" => DecodeAp(DecryptAtbash(ReadCipherText(env)), env.PlainLength),
            "VIGENERE" => DecodeAp(DecryptVigenere(ReadCipherText(env), key.TextValue ?? throw Missing(key, "TextValue")), env.PlainLength),
            "PLAYFAIR" => DecodeAp(DecryptPlayfair(ReadCipherText(env), key.TextValue ?? "ASFI"), env.PlainLength),
            "HILL" => DecodeAp(DecryptHill(ReadCipherText(env), key.Matrix ?? throw Missing(key, "Matrix[4]")), env.PlainLength),
            "DES" => DecryptDes(ReadBytes(env.Payload, env.Encoding), RequireBytes(key), RequireIv(env), false),
            "3DES" => DecryptDes(ReadBytes(env.Payload, env.Encoding), RequireBytes(key), RequireIv(env), true),
            "BLOWFISH" => DecryptBcCbc(new BlowfishEngine(), ReadBytes(env.Payload, env.Encoding), RequireBytes(key), RequireIv(env)),
            "TWOFISH" => DecryptBcCbc(new TwofishEngine(), ReadBytes(env.Payload, env.Encoding), RequireBytes(key), RequireIv(env)),
            "AES" => DecryptAesGcm(ReadBytes(env.Payload, env.Encoding), RequireBytes(key), RequireNonce(env), RequireTag(env)),
            "RSA" => DecryptHybridRsa(env, key),
            "ELGAMAL" => DecryptHybridElGamal(env, key),
            "ECC" => DecryptHybridEcc(env, key),
            "CHACHA20" => DecryptChaCha(env, key),
            _ => throw new NotSupportedException($"Algoritmo no soportado: {env.Algorithm}")
        };
    }

    private static Exception Missing(CryptoKeyEntry key, string field) => new InvalidOperationException($"La llave {key.KeyId} requiere {field}.");
    private static byte[] RequireBytes(CryptoKeyEntry key) => !string.IsNullOrWhiteSpace(key.ValueBase64) ? Convert.FromBase64String(key.ValueBase64) : throw Missing(key, "ValueBase64");
    private static byte[] RequireIv(CryptoEnvelopeDto env) => DecodeBase64Field(env.IvBase64, "ivBase64");
    private static byte[] RequireNonce(CryptoEnvelopeDto env) => DecodeBase64Field(env.NonceBase64, "nonceBase64");
    private static byte[] RequireTag(CryptoEnvelopeDto env) => DecodeBase64Field(env.TagBase64, "tagBase64");
    private static byte[] RequireAux(CryptoEnvelopeDto env) => DecodeBase64Field(env.AuxiliaryBase64, "auxiliaryBase64");
    private static byte[] DecodeBase64Field(string? value, string field) => !string.IsNullOrWhiteSpace(value) ? Convert.FromBase64String(value) : throw new InvalidOperationException($"El sobre criptográfico requiere {field}.");

    private static string ReadCipherText(CryptoEnvelopeDto env)
    {
        if (string.Equals(env.Encoding, "base64", StringComparison.OrdinalIgnoreCase))
            return Encoding.UTF8.GetString(Convert.FromBase64String(env.Payload));
        return env.Payload;
    }

    private static byte[] ReadBytes(string payload, string? encoding)
    {
        if (string.Equals(encoding, "hex", StringComparison.OrdinalIgnoreCase)) return Convert.FromHexString(payload);
        try { return Convert.FromBase64String(payload); }
        catch (FormatException) { return Encoding.UTF8.GetBytes(payload); }
    }

    private static int Nibble(char c)
    {
        c = char.ToUpperInvariant(c);
        var i = HexAlphabet.IndexOf(c);
        if (i >= 0) return i;
        if (Uri.IsHexDigit(c)) return int.Parse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture) & 0xF;
        return c & 0xF;
    }

    private static char A16(int n) => HexAlphabet[((n % 16) + 16) % 16];

    private static byte[] DecodeAp(string encoded, int? plainLength)
    {
        var clean = new string(encoded.Where(c => HexAlphabet.Contains(char.ToUpperInvariant(c))).Select(char.ToUpperInvariant).ToArray());
        if (plainLength is > 0 && clean.Length > plainLength.Value * 2) clean = clean[..(plainLength.Value * 2)];
        if ((clean.Length & 1) != 0) throw new CryptographicException("Texto A..P cifrado inválido: longitud impar.");
        var data = new byte[clean.Length / 2];
        for (var i = 0; i < data.Length; i++) data[i] = (byte)((Nibble(clean[i * 2]) << 4) | Nibble(clean[i * 2 + 1]));
        if (plainLength is > 0 && data.Length > plainLength.Value) Array.Resize(ref data, plainLength.Value);
        return data;
    }

    private static string DecryptCaesar(string text, int shift) => new(text.Select(c => A16(Nibble(c) - shift)).ToArray());
    private static string DecryptAtbash(string text) => new(text.Select(c => A16(15 - Nibble(c))).ToArray());

    private static int[] KeyNibbles(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new InvalidOperationException("Clave vacía.");
        return key.ToUpperInvariant().Where(char.IsLetter).Select(c => (c - 'A') % 16).ToArray();
    }

    private static string DecryptVigenere(string text, string key)
    {
        var k = KeyNibbles(key);
        var chars = text.Select((c, i) => A16(Nibble(c) - k[i % k.Length])).ToArray();
        return new string(chars);
    }

    private static char[] BuildPlayfairSquare(string key)
    {
        return (key.ToUpperInvariant() + HexAlphabet).Where(c => c is >= 'A' and <= 'P').Distinct().ToArray();
    }

    private static string DecryptPlayfair(string text, string key)
    {
        var sq = BuildPlayfairSquare(key);
        var pos = sq.Select((c, i) => (c, i)).ToDictionary(x => x.c, x => x.i);
        var clean = new string(text.Select(c => A16(Nibble(c))).ToArray());
        if ((clean.Length & 1) != 0) clean += "A";
        var sb = new StringBuilder(clean.Length);
        for (var i = 0; i < clean.Length; i += 2)
        {
            var p1 = pos[clean[i]]; var p2 = pos[clean[i + 1]];
            var r1 = p1 / 4; var c1 = p1 % 4; var r2 = p2 / 4; var c2 = p2 % 4;
            if (r1 == r2) { c1 = (c1 + 3) % 4; c2 = (c2 + 3) % 4; }
            else if (c1 == c2) { r1 = (r1 + 3) % 4; r2 = (r2 + 3) % 4; }
            else { (c1, c2) = (c2, c1); }
            sb.Append(sq[r1 * 4 + c1]).Append(sq[r2 * 4 + c2]);
        }
        return sb.ToString();
    }

    private static string DecryptHill(string text, int[] m)
    {
        if (m.Length != 4) throw new InvalidOperationException("Hill requiere Matrix con 4 enteros [a,b,c,d].");
        var det = Mod(m[0] * m[3] - m[1] * m[2], 16);
        var invDet = -1;
        for (var candidate = 0; candidate < 16; candidate++)
        {
            if (Mod(det * candidate, 16) == 1) { invDet = candidate; break; }
        }
        if (invDet < 0) throw new CryptographicException("La matriz Hill no es invertible módulo 16.");
        var inv = new[] { Mod(invDet * m[3],16), Mod(-invDet * m[1],16), Mod(-invDet * m[2],16), Mod(invDet * m[0],16) };
        var clean = new string(text.Select(c => A16(Nibble(c))).ToArray());
        if ((clean.Length & 1) != 0) clean += "A";
        var sb = new StringBuilder(clean.Length);
        for (var i=0;i<clean.Length;i+=2)
        {
            var x = Nibble(clean[i]); var y=NibbbleSafe(clean[i+1]);
            sb.Append(A16(inv[0]*x + inv[1]*y)).Append(A16(inv[2]*x + inv[3]*y));
        }
        return sb.ToString();
    }
    private static int NibbbleSafe(char c) => Nibble(c);
    private static int Mod(int a, int m) => ((a % m) + m) % m;

    private static byte[] DecryptDes(byte[] cipher, byte[] key, byte[] iv, bool triple)
    {
        using SymmetricAlgorithm alg = triple ? TripleDES.Create() : DES.Create();
        alg.Key = key; alg.IV = iv; alg.Mode = CipherMode.CBC; alg.Padding = PaddingMode.PKCS7;
        using var dec = alg.CreateDecryptor();
        return dec.TransformFinalBlock(cipher, 0, cipher.Length);
    }

    private static byte[] DecryptBcCbc(IBlockCipher engine, byte[] cipherText, byte[] key, byte[] iv)
    {
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(engine), new Pkcs7Padding());
        cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));
        var output = new byte[cipher.GetOutputSize(cipherText.Length)];
        var len = cipher.ProcessBytes(cipherText, 0, cipherText.Length, output, 0);
        len += cipher.DoFinal(output, len);
        return output[..len];
    }

    private static byte[] DecryptAesGcm(byte[] cipher, byte[] key, byte[] nonce, byte[] tag)
    {
        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(key, tag.Length);
        aes.Decrypt(nonce, cipher, tag, plain);
        return plain;
    }

    private static byte[] DecryptHybridRsa(CryptoEnvelopeDto env, CryptoKeyEntry key)
    {
        if (string.IsNullOrWhiteSpace(key.PrivateKeyPem)) throw Missing(key, "PrivateKeyPem");
        using var rsa = RSA.Create();
        rsa.ImportFromPem(key.PrivateKeyPem);
        var sessionKey = rsa.Decrypt(RequireAux(env), RSAEncryptionPadding.OaepSHA256);
        return DecryptAesGcm(ReadBytes(env.Payload, env.Encoding), sessionKey, RequireNonce(env), RequireTag(env));
    }

    private static byte[] DecryptHybridElGamal(CryptoEnvelopeDto env, CryptoKeyEntry key)
    {
        AsymmetricKeyParameter privateKey;
        if (key.PHex is not null && key.GHex is not null && key.XHex is not null)
        {
            privateKey = new ElGamalPrivateKeyParameters(
                new Org.BouncyCastle.Math.BigInteger(key.XHex, 16),
                new ElGamalParameters(new Org.BouncyCastle.Math.BigInteger(key.PHex, 16), new Org.BouncyCastle.Math.BigInteger(key.GHex, 16)));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(key.PrivateKeyPem)) throw Missing(key, "PrivateKeyPem o PHex/GHex/XHex");
            using var sr = new StringReader(key.PrivateKeyPem);
            var obj = new PemReader(sr).ReadObject();
            privateKey = obj switch
            {
                AsymmetricCipherKeyPair kp => kp.Private,
                AsymmetricKeyParameter akp when akp.IsPrivate => akp,
                _ => throw new CryptographicException("PEM ElGamal privado no reconocido.")
            };
        }
        var engine = new ElGamalEngine();
        engine.Init(false, privateKey);
        var wrapped = RequireAux(env);
        var sessionKey = engine.ProcessBlock(wrapped, 0, wrapped.Length);
        if (sessionKey.Length > 32) sessionKey = sessionKey[^32..];
        if (sessionKey.Length < 32) sessionKey = new byte[32-sessionKey.Length].Concat(sessionKey).ToArray();
        return DecryptAesGcm(ReadBytes(env.Payload, env.Encoding), sessionKey, RequireNonce(env), RequireTag(env));
    }

    private static byte[] DecryptHybridEcc(CryptoEnvelopeDto env, CryptoKeyEntry key)
    {
        if (string.IsNullOrWhiteSpace(key.PrivateKeyPem)) throw Missing(key, "PrivateKeyPem");
        using var own = ECDiffieHellman.Create();
        own.ImportFromPem(key.PrivateKeyPem);
        using var ephemeral = ECDiffieHellman.Create();
        ephemeral.ImportSubjectPublicKeyInfo(RequireAux(env), out _);
        var sessionKey = own.DeriveKeyFromHash(ephemeral.PublicKey, HashAlgorithmName.SHA256);
        return DecryptAesGcm(ReadBytes(env.Payload, env.Encoding), sessionKey, RequireNonce(env), RequireTag(env));
    }

    private static byte[] DecryptChaCha(CryptoEnvelopeDto env, CryptoKeyEntry key)
    {
        var cipher = ReadBytes(env.Payload, env.Encoding);
        var tag = RequireTag(env);
        var plain = new byte[cipher.Length];
        using var chacha = new ChaCha20Poly1305(RequireBytes(key));
        chacha.Decrypt(RequireNonce(env), cipher, tag, plain);
        return plain;
    }
}
