namespace TableOrder.Client;

// 端末の鍵 (P-256)。秘密鍵は端末の外へ出さず (Android は Keystore、ブラウザは取り出せない WebCrypto の鍵に作る)、公開鍵と署名だけを返す
// 値は公開鍵を SubjectPublicKeyInfo、署名を DER にし (ほかの形を返す鍵の実装は DeviceCredentials で直す)、API の形 (JWK、JWT) には DeviceCredentials で直す
// 公開鍵と署名は非同期にする (ブラウザの鍵は非同期でしか使えない。時間のかかる鍵の操作を画面のスレッドで待たない)
public interface IDeviceKey
{
    // 鍵がなければ作り、公開鍵を返す
    ValueTask<byte[]> GetPublicKeyAsync();

    // SHA-256 の ECDSA で署名する
    ValueTask<byte[]> SignAsync(byte[] data);

    // 鍵を消す (端末を無効にされたとき。次に使うときに作り直す)
    void Delete();
}
