namespace TableOrder.Client;

using System.Security.Cryptography;

// テストの端末の鍵。Android の Keystore と同じ形 (公開鍵は SubjectPublicKeyInfo、署名は DER) で返す
public sealed class TestDeviceKey : IDeviceKey
{
    private ECParameters parameters = Generate();

    // 公開鍵の点 (JWK の x と y と比べる)
    public ECPoint PublicPoint => parameters.Q;

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

    // JWS の形 (r と s を並べたもの) の署名を確かめる
    public bool Verify(byte[] data, byte[] signature)
    {
        using var key = ECDsa.Create(parameters);
        return key.VerifyData(data, signature, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
    }

    private static ECParameters Generate()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return key.ExportParameters(true);
    }
}
