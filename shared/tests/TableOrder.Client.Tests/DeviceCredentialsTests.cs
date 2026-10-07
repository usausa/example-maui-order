namespace TableOrder.Client;

using System.Buffers.Text;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public sealed class DeviceCredentialsTests
{
    //--------------------------------------------------------------------------------
    // Public key
    //--------------------------------------------------------------------------------

    // 公開鍵 (SubjectPublicKeyInfo) から JWK の x と y (32 バイトずつ) を取り出す
    [Fact]
    public void CreatePublicKeyFromSubjectPublicKeyInfo()
    {
        // Arrange
        var key = new TestDeviceKey();

        // Act
        var jwk = DeviceCredentials.CreatePublicKey(key.GetPublicKey());

        // Assert
        Assert.Equal("EC", jwk.Kty);
        Assert.Equal("P-256", jwk.Crv);
        Assert.Equal(key.PublicPoint.X, Base64Url.DecodeFromChars(jwk.X));
        Assert.Equal(key.PublicPoint.Y, Base64Url.DecodeFromChars(jwk.Y));
    }

    // P-256 でない鍵は受けない
    [Fact]
    public void CreatePublicKeyRejectsOtherCurve()
    {
        // Arrange
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP384);
        var info = key.ExportSubjectPublicKeyInfo();

        // Act / Assert
        Assert.Throws<CryptographicException>(() => DeviceCredentials.CreatePublicKey(info));
    }

    //--------------------------------------------------------------------------------
    // Assertion
    //--------------------------------------------------------------------------------

    // トークンの要求は ES256 の JWT (iss と sub は端末の id、aud はサーバの名前、期限は 2 分、使い捨ての jti) で、端末の鍵で確かめられる
    [Fact]
    public void CreateAssertionSignedByDeviceKey()
    {
        // Arrange
        var key = new TestDeviceKey();
        var deviceId = Guid.CreateVersion7();
        var now = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

        // Act
        var assertion = DeviceCredentials.CreateAssertion(key, deviceId, now);
        var next = DeviceCredentials.CreateAssertion(key, deviceId, now);

        // Assert
        var parts = assertion.Split('.');
        Assert.Equal(3, parts.Length);
        using var header = ParseJson(parts[0]);
        Assert.Equal("ES256", header.RootElement.GetProperty("alg").GetString());
        using var payload = ParseJson(parts[1]);
        var claims = payload.RootElement;
        Assert.Equal(deviceId.ToString("D"), claims.GetProperty("iss").GetString());
        Assert.Equal(deviceId.ToString("D"), claims.GetProperty("sub").GetString());
        Assert.Equal(DeviceCredentials.Audience, claims.GetProperty("aud").GetString());
        Assert.Equal(now.ToUnixTimeSeconds(), claims.GetProperty("iat").GetInt64());
        Assert.Equal(now.AddMinutes(2).ToUnixTimeSeconds(), claims.GetProperty("exp").GetInt64());
        using var nextPayload = ParseJson(next.Split('.')[1]);
        Assert.NotEqual(claims.GetProperty("jti").GetString(), nextPayload.RootElement.GetProperty("jti").GetString());
        Assert.True(key.Verify(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), Base64Url.DecodeFromChars(parts[2])));
    }

    // DER の署名の r と s を 32 バイトずつに揃える (符号のための先頭の 0 を除き、短い値は前を 0 で埋める)
    [Fact]
    public void CreateAssertionAlignsSignatureValues()
    {
        // Arrange
        var r = Enumerable.Range(0, 32).Select(static x => (byte)(0x80 + x)).ToArray();
        var s = new byte[] { 0x01, 0x02 };
        var key = new FixedSignatureKey(r, s);

        // Act
        var assertion = DeviceCredentials.CreateAssertion(key, Guid.CreateVersion7(), DateTimeOffset.UtcNow);

        // Assert
        var signature = Base64Url.DecodeFromChars(assertion.Split('.')[2]);
        Assert.Equal(64, signature.Length);
        Assert.Equal(r, signature[..32]);
        Assert.Equal(new byte[30].Concat(s), signature[32..]);
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private static JsonDocument ParseJson(string part) =>
        JsonDocument.Parse(Base64Url.DecodeFromChars(part));

    // 決まった r と s の署名を DER で返す鍵
    private sealed class FixedSignatureKey : IDeviceKey
    {
        private readonly byte[] r;

        private readonly byte[] s;

        public FixedSignatureKey(byte[] r, byte[] s)
        {
            this.r = r;
            this.s = s;
        }

        public byte[] GetPublicKey() => throw new NotSupportedException();

        public byte[] Sign(byte[] data)
        {
            var writer = new AsnWriter(AsnEncodingRules.DER);
            using (writer.PushSequence())
            {
                writer.WriteIntegerUnsigned(r);
                writer.WriteIntegerUnsigned(s);
            }

            return writer.Encode();
        }

        public void Delete()
        {
        }
    }
}
