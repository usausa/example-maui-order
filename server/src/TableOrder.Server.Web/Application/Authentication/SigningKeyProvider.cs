namespace TableOrder.Server.Web.Application.Authentication;

using System.Security.Cryptography;

using Microsoft.IdentityModel.Tokens;

// アクセストークンの署名の鍵 (ES256)。鍵は環境ごとに 1 つで、テナントごとには分けない
public sealed class SigningKeyProvider : IDisposable
{
    private readonly ECDsa algorithm;

    public ECDsaSecurityKey SecurityKey { get; }

    public SigningCredentials Credentials { get; }

    public SigningKeyProvider(
        ILogger<SigningKeyProvider> log,
        IHostEnvironment environment,
        TokenSetting setting)
    {
        if (!String.IsNullOrEmpty(setting.SigningKey))
        {
            algorithm = ECDsa.Create();
            algorithm.ImportFromPem(setting.SigningKey);
        }
        else if (environment.IsDevelopment())
        {
            // 開発の環境は起動のたびに鍵を作る (起動し直すと出したトークンは通らなくなり、端末はトークンを取り直す)
            algorithm = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            log.WarnEphemeralSigningKey();
        }
        else
        {
            throw new InvalidOperationException("Token:SigningKey is not configured.");
        }

        SecurityKey = new ECDsaSecurityKey(algorithm);
        // 鍵を替える間に古い鍵と新しい鍵を見分けられるように、鍵の指紋を kid にする
        SecurityKey.KeyId = Base64UrlEncoder.Encode(SecurityKey.ComputeJwkThumbprint());
        Credentials = new SigningCredentials(SecurityKey, SecurityAlgorithms.EcdsaSha256);
    }

    public void Dispose()
    {
        algorithm.Dispose();
    }
}
