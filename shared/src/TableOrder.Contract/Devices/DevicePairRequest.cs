namespace TableOrder.Contract.Devices;

// 端末の登録。ペアリングコードか登録トークンのどちらかと、端末の中で作った鍵の公開鍵を送る
public sealed class DevicePairRequest
{
    // 管理画面で出す 6 桁の数字
    public string? PairingCode { get; set; }

    // EMM が管理対象の構成で配る値
    public string? EnrollmentToken { get; set; }

    public DevicePublicKey PublicKey { get; set; } = default!;

    public string DeviceName { get; set; } = default!;

    public string? AppVersion { get; set; }
}

// 端末の公開鍵 (P-256 の JWK。秘密鍵の値は送らない)
public sealed class DevicePublicKey
{
    public string Kty { get; set; } = default!;

    public string Crv { get; set; } = default!;

    public string X { get; set; } = default!;

    public string Y { get; set; } = default!;
}
