namespace TableOrder.Terminal.Components;

using System.Security.Cryptography;

using Android.Runtime;
using Android.Security.Keystore;

using Java.Security;
using Java.Security.Spec;

// 鍵がないことと Keystore の失敗 (Java の例外) は CryptographicException にする
// (窓口は鍵の失敗を通信できないものとして返し、つなぎ直しや状態の報告を例外で止めない)
public sealed partial class DeviceKey
{
    private const string KeyStoreProvider = "AndroidKeyStore";

    private const string SignatureAlgorithm = "SHA256withECDSA";

    // 公開鍵は鍵の証明書から X.509 の形 (SubjectPublicKeyInfo) で読む
    private static partial byte[] ReadPublicKey() =>
        Guard(static () =>
        {
            using var store = LoadStore();
            if (!store.ContainsAlias(Alias))
            {
                Generate();
            }

            using var certificate = store.GetCertificate(Alias) ?? throw new CryptographicException("Device key certificate is not found.");
            using var publicKey = certificate.PublicKey ?? throw new CryptographicException("Device public key is not found.");
            return publicKey.GetEncoded() ?? throw new CryptographicException("Device public key is not encodable.");
        });

    // 署名は DER (ASN.1 の r と s) で返る
    private static partial byte[] SignData(byte[] data) =>
        Guard(() =>
        {
            using var store = LoadStore();
            using var entry = store.GetKey(Alias, null) ?? throw new CryptographicException("Device key is not found.");
            using var key = entry.JavaCast<IPrivateKey>() ?? throw new CryptographicException("Device key is not a private key.");
            using var signature = Signature.GetInstance(SignatureAlgorithm) ?? throw new CryptographicException("Signature algorithm is not supported.");
            signature.InitSign(key);
            signature.Update(data);
            return signature.Sign() ?? throw new CryptographicException("Signature is empty.");
        });

    private static partial void DeleteKey() =>
        Guard(static () =>
        {
            using var store = LoadStore();
            if (store.ContainsAlias(Alias))
            {
                store.DeleteEntry(Alias);
            }
        });

    private static T Guard<T>(Func<T> operation)
    {
        try
        {
            return operation();
        }
        catch (Java.Lang.Exception ex)
        {
            throw new CryptographicException("Android key store operation failed.", ex);
        }
    }

    private static void Guard(Action operation)
    {
        try
        {
            operation();
        }
        catch (Java.Lang.Exception ex)
        {
            throw new CryptographicException("Android key store operation failed.", ex);
        }
    }

    private static KeyStore LoadStore()
    {
        var store = KeyStore.GetInstance(KeyStoreProvider) ?? throw new CryptographicException("Android key store is not available.");
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

        using var generator = KeyPairGenerator.GetInstance(KeyProperties.KeyAlgorithmEc, KeyStoreProvider) ?? throw new CryptographicException("Key generator is not available.");
        generator.Initialize(spec);
        using var pair = generator.GenerateKeyPair();
    }
}
