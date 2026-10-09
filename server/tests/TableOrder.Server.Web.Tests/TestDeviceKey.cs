namespace TableOrder.Server.Web;

using System.Security.Cryptography;

using TableOrder.Client;

// テストの端末の鍵 (Android の Keystore と同じく、公開鍵は SubjectPublicKeyInfo、署名は DER で返す)
public sealed class TestDeviceKey : IDeviceKey
{
    private ECParameters parameters = Generate();

    public ValueTask<byte[]> GetPublicKeyAsync()
    {
        using var key = ECDsa.Create(parameters);
        return ValueTask.FromResult(key.ExportSubjectPublicKeyInfo());
    }

    public ValueTask<byte[]> SignAsync(byte[] data)
    {
        using var key = ECDsa.Create(parameters);
        return ValueTask.FromResult(key.SignData(data, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence));
    }

    public void Delete() => parameters = Generate();

    private static ECParameters Generate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportParameters(true);
    }
}
