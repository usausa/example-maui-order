namespace TableOrder.Server.Web;

using System.Buffers.Binary;
using System.Security.Cryptography;

// 認証アプリの代わり。鍵 (Base32) から今のワンタイムコード (TOTP、30 秒、6 桁) を出す
// TOTP の HMAC は、認証アプリとサーバ (ASP.NET Core Identity) が合わせている SHA-1 にする
public static class TestTotp
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    public static string Compute(string key, DateTimeOffset now)
    {
        var counter = new byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, now.ToUnixTimeSeconds() / 30);
        using var hmac = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA1, DecodeBase32(key));
        hmac.AppendData(counter);
        var hash = hmac.GetHashAndReset();
        var offset = hash[^1] & 0x0F;
        var code = (BinaryPrimitives.ReadInt32BigEndian(hash.AsSpan(offset)) & 0x7FFFFFFF) % 1_000_000;
        return code.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] DecodeBase32(string key)
    {
        var bits = 0;
        var value = 0;
        var output = new List<byte>();
        foreach (var c in key.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant())
        {
            value = ((value << 5) | Alphabet.IndexOf(c, StringComparison.Ordinal)) & 0xFFFF;
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(value >> (bits - 8)));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
