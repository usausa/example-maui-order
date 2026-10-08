namespace TableOrder.Domain;

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

// スタッフの PIN のハッシュ (PBKDF2-HMAC-SHA256)。サーバが店舗の設定で作って端末に渡し、端末は入れた PIN を同じ計算で確かめる
// 端末は平文を持たないので、サーバにつながらないときも保存したハッシュで確かめられる
public static class StaffPins
{
    // サーバが作るときの回数 (確かめるときは受け取った回数を使う)
    public const int Iterations = 100_000;

    // 受け取った回数の上限 (大きすぎる回数で端末を止めない)
    private const int MaxIterations = 1_000_000;

    private const int SaltSize = 16;

    private const int HashSize = 32;

    // 決まった桁数の数字
    public static bool IsValid([NotNullWhen(true)] string? pin) =>
        (pin is { Length: Length.StaffPinDigits }) && pin.All(Char.IsAsciiDigit);

    // 塩を作ってハッシュにする (塩とハッシュは Base64)
    public static (string Salt, string Hash) Create(string pin, int iterations = Iterations)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return (Convert.ToBase64String(salt), Convert.ToBase64String(Derive(pin, salt, iterations)));
    }

    // 入れた PIN が合うか。形の崩れた塩とハッシュ、範囲の外の回数は合わないものにする
    public static bool Verify(string pin, int iterations, string salt, string hash)
    {
        if (iterations is <= 0 or > MaxIterations)
        {
            return false;
        }

        byte[] saltBytes;
        byte[] expected;
        try
        {
            saltBytes = Convert.FromBase64String(salt);
            expected = Convert.FromBase64String(hash);
        }
        catch (FormatException)
        {
            return false;
        }

        return (expected.Length == HashSize) && CryptographicOperations.FixedTimeEquals(Derive(pin, saltBytes, iterations), expected);
    }

    private static byte[] Derive(string pin, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, salt, iterations, HashAlgorithmName.SHA256, HashSize);
}
