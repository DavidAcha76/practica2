
using System.Security.Cryptography;
using ChaCha20Poly1305 = System.Security.Cryptography.ChaCha20Poly1305;
using System.Text;
using Microsoft.Extensions.Configuration;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Utilities.Encoders;

namespace BankApi.Shared;

public interface IAccountCipher
{
    CryptoEnvelope Encrypt(string plaintext);
    string Decrypt(CryptoEnvelope envelope); // usado por pruebas académicas; NO se expone como endpoint
}

internal abstract class ArmoredCipherBase : IAccountCipher
{
    public CryptoEnvelope Encrypt(string plaintext) => new() { CipherText = EncryptArmored(TextArmor.Encode(plaintext)) };
    public string Decrypt(CryptoEnvelope envelope) => TextArmor.Decode(DecryptArmored(envelope.CipherText));
    protected abstract string EncryptArmored(string input);
    protected abstract string DecryptArmored(string input);
}

internal sealed class CaesarCipher(int shift) : ArmoredCipherBase
{
    private readonly int _shift = ((shift % 16) + 16) % 16;
    protected override string EncryptArmored(string input) => Transform(input, _shift);
    protected override string DecryptArmored(string input) => Transform(input, 16 - _shift);
    private static string Transform(string s, int shift) => new(s.Select(c => (char)('A' + ((c - 'A' + shift) % 16))).ToArray());
}

internal sealed class AtbashCipher : ArmoredCipherBase
{
    protected override string EncryptArmored(string input) => Transform(input);
    protected override string DecryptArmored(string input) => Transform(input);
    private static string Transform(string s) => new(s.Select(c => (char)('A' + (15 - (c - 'A')))).ToArray());
}

internal sealed class VigenereCipher(string keyword) : ArmoredCipherBase
{
    private readonly int[] _key = string.IsNullOrWhiteSpace(keyword)
        ? [1]
        : keyword.ToUpperInvariant().Where(char.IsLetter).Select(c => (c - 'A') % 16).ToArray();
    protected override string EncryptArmored(string input) => Transform(input, false);
    protected override string DecryptArmored(string input) => Transform(input, true);
    private string Transform(string s, bool decrypt)
    {
        var chars = new char[s.Length];
        for (var i = 0; i < s.Length; i++)
        {
            var k = _key[i % _key.Length];
            if (decrypt) k = 16 - k;
            chars[i] = (char)('A' + ((s[i] - 'A' + k) % 16));
        }
        return new string(chars);
    }
}

internal sealed class PlayfairCipher : ArmoredCipherBase
{
    private readonly char[] _square;
    private readonly Dictionary<char, (int r, int c)> _pos = new();

    public PlayfairCipher(string keyword)
    {
        _square = BuildSquare(keyword);
        for (var i = 0; i < _square.Length; i++) _pos[_square[i]] = (i / 4, i % 4);
    }

    protected override string EncryptArmored(string input) => Transform(input, 1);
    protected override string DecryptArmored(string input) => Transform(input, -1);

    private string Transform(string input, int direction)
    {
        if (input.Length % 2 != 0) throw new InvalidOperationException("Playfair requiere longitud par.");
        var output = new char[input.Length];
        for (var i = 0; i < input.Length; i += 2)
        {
            var a = _pos[input[i]]; var b = _pos[input[i + 1]];
            if (a.r == b.r)
            {
                output[i] = _square[a.r * 4 + Mod(a.c + direction, 4)];
                output[i + 1] = _square[b.r * 4 + Mod(b.c + direction, 4)];
            }
            else if (a.c == b.c)
            {
                output[i] = _square[Mod(a.r + direction, 4) * 4 + a.c];
                output[i + 1] = _square[Mod(b.r + direction, 4) * 4 + b.c];
            }
            else
            {
                output[i] = _square[a.r * 4 + b.c];
                output[i + 1] = _square[b.r * 4 + a.c];
            }
        }
        return new string(output);
    }

    private static char[] BuildSquare(string keyword)
    {
        const string alphabet = "ABCDEFGHIJKLMNOP";
        return (keyword.ToUpperInvariant() + alphabet)
            .Where(c => c is >= 'A' and <= 'P')
            .Distinct()
            .Concat(alphabet.Where(c => !(keyword.ToUpperInvariant()).Contains(c)))
            .Distinct().Take(16).ToArray();
    }
    private static int Mod(int x, int m) => ((x % m) + m) % m;
}

internal sealed class HillCipher : ArmoredCipherBase
{
    // Matriz 2x2 sobre Z16: [[3,3],[2,5]], det=9, invertible mod 16.
    private static readonly int[,] M = { { 3, 3 }, { 2, 5 } };
    private static readonly int[,] Inv = { { 13, 5 }, { 14, 11 } };
    protected override string EncryptArmored(string input) => Transform(input, M);
    protected override string DecryptArmored(string input) => Transform(input, Inv);
    private static string Transform(string s, int[,] matrix)
    {
        if (s.Length % 2 != 0) throw new InvalidOperationException("Hill requiere longitud par.");
        var o = new char[s.Length];
        for (var i = 0; i < s.Length; i += 2)
        {
            var a = s[i] - 'A'; var b = s[i + 1] - 'A';
            o[i] = (char)('A' + ((matrix[0,0] * a + matrix[0,1] * b) % 16));
            o[i + 1] = (char)('A' + ((matrix[1,0] * a + matrix[1,1] * b) % 16));
        }
        return new string(o);
    }
}

internal sealed class DesCipher(byte[] key, bool triple) : IAccountCipher
{
    private readonly byte[] _key = key;
    private readonly bool _triple = triple;
    public CryptoEnvelope Encrypt(string plaintext)
    {
        using SymmetricAlgorithm alg = _triple ? TripleDES.Create() : DES.Create();
        alg.Mode = CipherMode.CBC; alg.Padding = PaddingMode.PKCS7; alg.Key = _key; alg.GenerateIV();
        using var enc = alg.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
        return new() { CipherText = Convert.ToBase64String(cipher), Metadata = new() { ["iv"] = Convert.ToBase64String(alg.IV), ["mode"] = "CBC-PKCS7" } };
    }
    public string Decrypt(CryptoEnvelope e)
    {
        using SymmetricAlgorithm alg = _triple ? TripleDES.Create() : DES.Create();
        alg.Mode = CipherMode.CBC; alg.Padding = PaddingMode.PKCS7; alg.Key = _key; alg.IV = Convert.FromBase64String(e.Metadata["iv"]);
        using var dec = alg.CreateDecryptor(); var c = Convert.FromBase64String(e.CipherText);
        return Encoding.UTF8.GetString(dec.TransformFinalBlock(c,0,c.Length));
    }
}

internal sealed class BouncyBlockCipher(byte[] key, string kind) : IAccountCipher
{
    private readonly byte[] _key = key; private readonly string _kind = kind;
    public CryptoEnvelope Encrypt(string plaintext)
    {
        var ivSize = _kind.Equals("Blowfish", StringComparison.OrdinalIgnoreCase) ? 8 : 16;
        var iv = RandomNumberGenerator.GetBytes(ivSize);
        var cipher = Build(true, iv); var input = Encoding.UTF8.GetBytes(plaintext);
        var output = new byte[cipher.GetOutputSize(input.Length)];
        var len = cipher.ProcessBytes(input,0,input.Length,output,0); len += cipher.DoFinal(output,len);
        return new() { CipherText = Convert.ToBase64String(output,0,len), Metadata = new() { ["iv"] = Convert.ToBase64String(iv), ["mode"] = "CBC-PKCS7" } };
    }
    public string Decrypt(CryptoEnvelope e)
    {
        var iv = Convert.FromBase64String(e.Metadata["iv"]); var cipher = Build(false, iv); var input = Convert.FromBase64String(e.CipherText);
        var output = new byte[cipher.GetOutputSize(input.Length)]; var len = cipher.ProcessBytes(input,0,input.Length,output,0); len += cipher.DoFinal(output,len);
        return Encoding.UTF8.GetString(output,0,len);
    }
    private PaddedBufferedBlockCipher Build(bool encrypt, byte[] iv)
    {
        IBlockCipher engine = _kind.Equals("Blowfish", StringComparison.OrdinalIgnoreCase) ? new BlowfishEngine() : new TwofishEngine();
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(engine), new Pkcs7Padding());
        cipher.Init(encrypt, new ParametersWithIV(new KeyParameter(_key), iv)); return cipher;
    }
}

internal sealed class AesGcmCipher(byte[] key) : IAccountCipher
{
    private readonly byte[] _key = key;
    public CryptoEnvelope Encrypt(string plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var p = Encoding.UTF8.GetBytes(plaintext); var c = new byte[p.Length];
        using var aes = new AesGcm(_key, 16); aes.Encrypt(nonce,p,c,tag);
        return new() { CipherText = Convert.ToBase64String(c), Metadata = new() { ["nonce"] = Convert.ToBase64String(nonce), ["tag"] = Convert.ToBase64String(tag), ["mode"]="AES-256-GCM" } };
    }
    public string Decrypt(CryptoEnvelope e)
    {
        var c=Convert.FromBase64String(e.CipherText); var p=new byte[c.Length]; using var aes=new AesGcm(_key,16);
        aes.Decrypt(Convert.FromBase64String(e.Metadata["nonce"]),c,Convert.FromBase64String(e.Metadata["tag"]),p); return Encoding.UTF8.GetString(p);
    }
}

internal sealed class ChaChaCipher(byte[] key) : IAccountCipher
{
    private readonly byte[] _key = key;
    public CryptoEnvelope Encrypt(string plaintext)
    {
        var nonce=RandomNumberGenerator.GetBytes(12); var tag=new byte[16]; var p=Encoding.UTF8.GetBytes(plaintext); var c=new byte[p.Length];
        using var a=new ChaCha20Poly1305(_key); a.Encrypt(nonce,p,c,tag);
        return new(){CipherText=Convert.ToBase64String(c),Metadata=new(){["nonce"]=Convert.ToBase64String(nonce),["tag"]=Convert.ToBase64String(tag),["mode"]="ChaCha20-Poly1305"}};
    }
    public string Decrypt(CryptoEnvelope e)
    {
        var c=Convert.FromBase64String(e.CipherText); var p=new byte[c.Length]; using var a=new ChaCha20Poly1305(_key);
        a.Decrypt(Convert.FromBase64String(e.Metadata["nonce"]),c,Convert.FromBase64String(e.Metadata["tag"]),p); return Encoding.UTF8.GetString(p);
    }
}

internal sealed class RsaHybridCipher(string publicKeyPath, string? privateKeyPath) : IAccountCipher
{
    public CryptoEnvelope Encrypt(string plaintext)
    {
        var dataKey=RandomNumberGenerator.GetBytes(32); var dataCipher=new AesGcmCipher(dataKey); var env=dataCipher.Encrypt(plaintext);
        using var rsa=RSA.Create(); rsa.ImportFromPem(File.ReadAllText(publicKeyPath)); var wrapped=rsa.Encrypt(dataKey,RSAEncryptionPadding.OaepSHA256);
        env.Metadata["wrappedKey"]=Convert.ToBase64String(wrapped); env.Metadata["scheme"]="RSA-OAEP-SHA256 + AES-256-GCM"; return env;
    }
    public string Decrypt(CryptoEnvelope e)
    {
        if(string.IsNullOrWhiteSpace(privateKeyPath)) throw new InvalidOperationException("No se configuró clave privada RSA.");
        using var rsa=RSA.Create(); rsa.ImportFromPem(File.ReadAllText(privateKeyPath)); var key=rsa.Decrypt(Convert.FromBase64String(e.Metadata["wrappedKey"]),RSAEncryptionPadding.OaepSHA256);
        return new AesGcmCipher(key).Decrypt(e);
    }
}

internal sealed class ElGamalHybridCipher : IAccountCipher
{
    private readonly ElGamalPublicKeyParameters _publicKey;
    private readonly ElGamalPrivateKeyParameters? _privateKey;

    public ElGamalHybridCipher(string pHex, string gHex, string yHex, string? xHex)
    {
        var parameters = new ElGamalParameters(new BigInteger(1, Hex.Decode(pHex)), new BigInteger(1, Hex.Decode(gHex)));
        _publicKey = new ElGamalPublicKeyParameters(new BigInteger(1, Hex.Decode(yHex)), parameters);
        if (!string.IsNullOrWhiteSpace(xHex)) _privateKey = new ElGamalPrivateKeyParameters(new BigInteger(1, Hex.Decode(xHex)), parameters);
    }

    public CryptoEnvelope Encrypt(string plaintext)
    {
        var dataKey=RandomNumberGenerator.GetBytes(32); var env=new AesGcmCipher(dataKey).Encrypt(plaintext); var engine=new ElGamalEngine();
        engine.Init(true,_publicKey); var wrapped=engine.ProcessBlock(dataKey,0,dataKey.Length);
        env.Metadata["wrappedKey"]=Convert.ToBase64String(wrapped); env.Metadata["scheme"]="ElGamal + AES-256-GCM"; return env;
    }
    public string Decrypt(CryptoEnvelope e)
    {
        if(_privateKey is null) throw new InvalidOperationException("No se configuró clave privada ElGamal."); var engine=new ElGamalEngine();
        engine.Init(false,_privateKey); var wrapped=Convert.FromBase64String(e.Metadata["wrappedKey"]); var key=engine.ProcessBlock(wrapped,0,wrapped.Length);
        if(key.Length<32){var k=new byte[32]; Buffer.BlockCopy(key,0,k,32-key.Length,key.Length); key=k;} return new AesGcmCipher(key).Decrypt(e);
    }
}

internal sealed class EccHybridCipher(string publicKeyPath, string? privateKeyPath) : IAccountCipher
{
    // La misma clave se usa para todo el lote; leerla desde disco por cada cuenta
    // convierte el cifrado ECC del banco 13 en un cuello de botella innecesario.
    private readonly string _publicKeyPem = File.ReadAllText(publicKeyPath);
    private readonly string? _privateKeyPem = string.IsNullOrWhiteSpace(privateKeyPath) ? null : File.ReadAllText(privateKeyPath);

    public CryptoEnvelope Encrypt(string plaintext)
    {
        using var recipient=ECDiffieHellman.Create(); recipient.ImportFromPem(_publicKeyPem);
        using var ephemeral=ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256); var key=ephemeral.DeriveKeyFromHash(recipient.PublicKey,HashAlgorithmName.SHA256);
        var env=new AesGcmCipher(key).Encrypt(plaintext); env.Metadata["ephemeralPublicKey"]=Convert.ToBase64String(ephemeral.ExportSubjectPublicKeyInfo()); env.Metadata["scheme"]="ECDH-P256 + SHA256 KDF + AES-256-GCM"; return env;
    }
    public string Decrypt(CryptoEnvelope e)
    {
        if(string.IsNullOrWhiteSpace(_privateKeyPem)) throw new InvalidOperationException("No se configuró clave privada ECC.");
        using var recipient=ECDiffieHellman.Create(); recipient.ImportFromPem(_privateKeyPem); using var ephemeral=ECDiffieHellman.Create(); ephemeral.ImportSubjectPublicKeyInfo(Convert.FromBase64String(e.Metadata["ephemeralPublicKey"]),out _);
        var key=recipient.DeriveKeyFromHash(ephemeral.PublicKey,HashAlgorithmName.SHA256); return new AesGcmCipher(key).Decrypt(e);
    }
}

public static class CipherFactory
{
    public static IAccountCipher Create(IConfiguration c)
    {
        var algorithm=c["Bank:Algorithm"] ?? throw new InvalidOperationException("Falta Bank:Algorithm");
        byte[] Key() => Convert.FromBase64String(c["Encryption:KeyBase64"] ?? "");
        return algorithm.ToUpperInvariant() switch
        {
            "CAESAR" => new CaesarCipher(int.Parse(c["Encryption:Shift"] ?? "3")),
            "ATBASH" => new AtbashCipher(),
            "VIGENERE" => new VigenereCipher(c["Encryption:Keyword"] ?? "BOLIVIA"),
            "PLAYFAIR" => new PlayfairCipher(c["Encryption:Keyword"] ?? "BANCOBCP"),
            "HILL" => new HillCipher(),
            "DES" => new DesCipher(Key(), false),
            "3DES" => new DesCipher(Key(), true),
            "BLOWFISH" => new BouncyBlockCipher(Key(), "Blowfish"),
            "TWOFISH" => new BouncyBlockCipher(Key(), "Twofish"),
            "AES" => new AesGcmCipher(Key()),
            "CHACHA20" => new ChaChaCipher(Key()),
            "RSA" => new RsaHybridCipher(Resolve(c["Encryption:PublicKeyFile"]!), ResolveNullable(c["Encryption:PrivateKeyFile"])),
            "ELGAMAL" => new ElGamalHybridCipher(c["Encryption:PHex"]!,c["Encryption:GHex"]!,c["Encryption:YHex"]!,c["Encryption:XHex"]),
            "ECC" => new EccHybridCipher(Resolve(c["Encryption:PublicKeyFile"]!), ResolveNullable(c["Encryption:PrivateKeyFile"])),
            _ => throw new NotSupportedException($"Algoritmo no soportado: {algorithm}")
        };
    }
    private static string Resolve(string path)
    {
        if (Path.IsPathRooted(path)) return path;
        var current = Path.GetFullPath(path, Directory.GetCurrentDirectory());
        if (File.Exists(current)) return current;
        return Path.GetFullPath(path, AppContext.BaseDirectory);
    }
    private static string? ResolveNullable(string? path) => string.IsNullOrWhiteSpace(path) ? null : Resolve(path);
}
