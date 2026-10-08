namespace TableOrder.Terminal.Components;

using Android.Runtime;
using Android.Security.Keystore;

using Java.Security;
using Java.Security.Spec;

public sealed partial class DeviceKey
{
    private const string KeyStoreProvider = "AndroidKeyStore";

    private const string SignatureAlgorithm = "SHA256withECDSA";

    // 公開鍵は鍵の証明書から X.509 の形 (SubjectPublicKeyInfo) で読む
    private static partial byte[] ReadPublicKey()
    {
        using var store = LoadStore();
        if (!store.ContainsAlias(Alias))
        {
            Generate();
        }

        using var certificate = store.GetCertificate(Alias) ?? throw new InvalidOperationException("Device key certificate is not found.");
        using var publicKey = certificate.PublicKey ?? throw new InvalidOperationException("Device public key is not found.");
        return publicKey.GetEncoded() ?? throw new InvalidOperationException("Device public key is not encodable.");
    }

    // 署名は DER (ASN.1 の r と s) で返る
    private static partial byte[] SignData(byte[] data)
    {
        using var store = LoadStore();
        using var entry = store.GetKey(Alias, null) ?? throw new InvalidOperationException("Device key is not found.");
        using var key = entry.JavaCast<IPrivateKey>() ?? throw new InvalidOperationException("Device key is not a private key.");
        using var signature = Signature.GetInstance(SignatureAlgorithm) ?? throw new InvalidOperationException("Signature algorithm is not supported.");
        signature.InitSign(key);
        signature.Update(data);
        return signature.Sign() ?? throw new InvalidOperationException("Signature is empty.");
    }

    private static partial void DeleteKey()
    {
        using var store = LoadStore();
        if (store.ContainsAlias(Alias))
        {
            store.DeleteEntry(Alias);
        }
    }

    private static KeyStore LoadStore()
    {
        var store = KeyStore.GetInstance(KeyStoreProvider) ?? throw new InvalidOperationException("Android key store is not available.");
        store.Load(null);
        return store;
    }

    // 署名だけに使う P-256 の鍵を Keystore の中に作る (秘密鍵はアプリにも渡らない)
    private static void Generate()
    {
        using var curve = new ECGenParameterSpec("secp256r1");
        using var builder = new KeyGenParameterSpec.Builder(Alias, KeyStorePurpose.Sign);
        builder.SetAlgorithmParameterSpec(curve);
        builder.SetDigests(KeyProperties.DigestSha256);
        using var spec = builder.Build();

        using var generator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmEc, KeyStoreProvider) ?? throw new InvalidOperationException("Key generator is not available.");
        generator.Initialize(spec);
        using var pair = generator.GenerateKeyPair();
    }
}
