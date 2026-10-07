namespace TableOrder.Client;

// 端末の鍵 (P-256)。秘密鍵は端末の外へ出さず (Android は Keystore に作る)、公開鍵と署名だけを返す
// 値はプラットフォームの API が返す形 (公開鍵は SubjectPublicKeyInfo、署名は DER) にし、API の形 (JWK、JWT) には DeviceCredentials で直す
public interface IDeviceKey
{
    // 鍵がなければ作り、公開鍵を返す
    byte[] GetPublicKey();

    // SHA-256 の ECDSA で署名する
    byte[] Sign(byte[] data);

    // 鍵を消す (端末を無効にされたとき。次に使うときに作り直す)
    void Delete();
}
