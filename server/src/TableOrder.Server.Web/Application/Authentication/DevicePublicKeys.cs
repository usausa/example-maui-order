namespace TableOrder.Server.Web.Application.Authentication;

using System.Security.Cryptography;
using System.Text.Json;

using Microsoft.IdentityModel.Tokens;

using TableOrder.Contract.Devices;

// 端末が送る公開鍵 (P-256 の JWK) の形を確かめ、保存する JSON にする
public static class DevicePublicKeys
{
    private const int CoordinateLength = 32;

    public static bool TryCreateJwk(DevicePublicKey? key, [NotNullWhen(true)] out string? jwk)
    {
        jwk = null;
        if ((key is null) || (key.Kty != JsonWebAlgorithmsKeyTypes.EllipticCurve) || (key.Crv != JsonWebKeyECTypes.P256) ||
            !TryDecode(key.X, out var x) || !TryDecode(key.Y, out var y))
        {
            return false;
        }

        // 曲線の上の点か (鍵として読めるか) を確かめる。読めない点は、環境によって暗号の例外か未対応の例外になる
        try
        {
            using var algorithm = ECDsa.Create(new ECParameters
            {
                Curve = ECCurve.NamedCurves.nistP256,
                Q = new ECPoint { X = x, Y = y }
            });
        }
        catch (Exception e) when (e is CryptographicException or PlatformNotSupportedException)
        {
            return false;
        }

        // 座標は読んだ値から書き直す (同じ鍵を同じ文字列で持ち、登録し直した端末を同じ鍵で引けるように)
        jwk = JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["kty"] = key.Kty,
            ["crv"] = key.Crv,
            ["x"] = Base64UrlEncoder.Encode(x),
            ["y"] = Base64UrlEncoder.Encode(y)
        });
        return true;
    }

    private static bool TryDecode(string? value, [NotNullWhen(true)] out byte[]? bytes)
    {
        bytes = null;
        if (String.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            bytes = Base64UrlEncoder.DecodeBytes(value);
        }
        catch (FormatException)
        {
            return false;
        }

        return bytes.Length == CoordinateLength;
    }
}
