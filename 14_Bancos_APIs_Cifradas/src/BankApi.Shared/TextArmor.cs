
using System.Text;

namespace BankApi.Shared;

internal static class TextArmor
{
    private const string Alphabet = "ABCDEFGHIJKLMNOP";

    public static string Encode(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = Alphabet[bytes[i] >> 4];
            chars[i * 2 + 1] = Alphabet[bytes[i] & 0x0F];
        }
        return new string(chars);
    }

    public static string Decode(string armored)
    {
        if (armored.Length % 2 != 0) throw new InvalidOperationException("Armor inválido.");
        var bytes = new byte[armored.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var hi = armored[i * 2] - 'A';
            var lo = armored[i * 2 + 1] - 'A';
            if (hi is < 0 or > 15 || lo is < 0 or > 15) throw new InvalidOperationException("Armor inválido.");
            bytes[i] = (byte)((hi << 4) | lo);
        }
        return Encoding.UTF8.GetString(bytes);
    }
}
