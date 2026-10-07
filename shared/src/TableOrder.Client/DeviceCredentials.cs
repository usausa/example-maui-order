namespace TableOrder.Client;

using System.Buffers;
using System.Buffers.Text;
using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// 端末の鍵の値を API の形にする (登録で送る公開鍵の JWK、トークンの要求の ES256 の JWT)
public static class DeviceCredentials
{
    // トークンの要求の宛先 (サーバの名前)
    public const string Audience = "tableorder";

    // P-256 の座標と、署名の r と s の長さ
    private const int FieldSize = 32;

    private const string EcPublicKeyOid = "1.2.840.10045.2.1";

    private const string P256Oid = "1.2.840.10045.3.1.7";

    // トークンの要求の期限 (サーバは 5 分より先の期限を受けない。端末の時計のずれを見込んで短くする)
    private static readonly TimeSpan AssertionLifetime = TimeSpan.FromMinutes(2);

    //--------------------------------------------------------------------------------
    // Public key
    //--------------------------------------------------------------------------------

    // 公開鍵 (SubjectPublicKeyInfo) を JWK にする
    public static DevicePublicKey CreatePublicKey(byte[] subjectPublicKeyInfo)
    {
        try
        {
            var reader = new AsnReader(subjectPublicKeyInfo, AsnEncodingRules.DER);
            var info = reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            var algorithm = info.ReadSequence();
            if ((algorithm.ReadObjectIdentifier() != EcPublicKeyOid) || (algorithm.ReadObjectIdentifier() != P256Oid))
            {
                throw new CryptographicException("The key is not a P-256 public key.");
            }

            // 圧縮しない点 (0x04、X、Y)
            var point = info.ReadBitString(out var unusedBits);
            info.ThrowIfNotEmpty();
            if ((unusedBits != 0) || (point.Length != 1 + (FieldSize * 2)) || (point[0] != 0x04))
            {
                throw new CryptographicException("The public key point is invalid.");
            }

            return new DevicePublicKey
            {
                Kty = "EC",
                Crv = "P-256",
                X = Base64Url.EncodeToString(point.AsSpan(1, FieldSize)),
                Y = Base64Url.EncodeToString(point.AsSpan(1 + FieldSize, FieldSize))
            };
        }
        catch (AsnContentException ex)
        {
            throw new CryptographicException("The public key is not a SubjectPublicKeyInfo.", ex);
        }
    }

    //--------------------------------------------------------------------------------
    // Assertion
    //--------------------------------------------------------------------------------

    // 鍵で署名したトークンの要求 (iss と sub は端末の id、aud はサーバの名前、使い捨ての jti)
    public static string CreateAssertion(IDeviceKey key, Guid deviceId, DateTimeOffset now)
    {
        var id = deviceId.ToString("D");
        var header = Encode(static writer =>
        {
            writer.WriteString("alg", "ES256");
            writer.WriteString("typ", "JWT");
        });
        var payload = Encode(writer =>
        {
            writer.WriteString("iss", id);
            writer.WriteString("sub", id);
            writer.WriteString("aud", Audience);
            writer.WriteNumber("iat", now.ToUnixTimeSeconds());
            writer.WriteNumber("exp", (now + AssertionLifetime).ToUnixTimeSeconds());
            writer.WriteString("jti", Guid.NewGuid().ToString("N"));
        });

        var input = $"{header}.{payload}";
        var signature = ToIeeeP1363(key.Sign(Encoding.ASCII.GetBytes(input)));
        return $"{input}.{Base64Url.EncodeToString(signature)}";
    }

    private static string Encode(Action<Utf8JsonWriter> write)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }

        return Base64Url.EncodeToString(buffer.WrittenSpan);
    }

    // DER の署名 (r と s の INTEGER の並び) を JWS の形 (r と s を 32 バイトずつ並べたもの) にする
    private static byte[] ToIeeeP1363(byte[] der)
    {
        try
        {
            var reader = new AsnReader(der, AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            var signature = new byte[FieldSize * 2];
            WriteInteger(sequence.ReadIntegerBytes().Span, signature.AsSpan(0, FieldSize));
            WriteInteger(sequence.ReadIntegerBytes().Span, signature.AsSpan(FieldSize, FieldSize));
            sequence.ThrowIfNotEmpty();
            return signature;
        }
        catch (AsnContentException ex)
        {
            throw new CryptographicException("The signature is not a DER sequence.", ex);
        }
    }

    // 符号のための先頭の 0 を除いて右に寄せる (短い値は前を 0 で埋める)
    private static void WriteInteger(ReadOnlySpan<byte> value, Span<byte> destination)
    {
        var trimmed = value.TrimStart((byte)0);
        if (trimmed.Length > destination.Length)
        {
            throw new CryptographicException("The signature value is too long.");
        }

        trimmed.CopyTo(destination[^trimmed.Length..]);
    }
}
