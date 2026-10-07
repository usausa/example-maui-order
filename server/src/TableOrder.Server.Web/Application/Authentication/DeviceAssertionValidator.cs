namespace TableOrder.Server.Web.Application.Authentication;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

// 端末の鍵で署名した使い捨ての JWT (トークンの要求) を確かめる
// iss と sub は端末の Id、aud はトークンを出すサーバ (Token:Issuer)、期限は 5 分以内、jti は使い捨て
public sealed class DeviceAssertionValidator
{
    private static readonly JsonWebTokenHandler Handler = new();

    private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    private readonly Lock sync = new();

    private readonly TimeProvider timeProvider;

    private readonly IMemoryCache cache;

    private readonly TokenSetting setting;

    public DeviceAssertionValidator(
        TimeProvider timeProvider,
        IMemoryCache cache,
        TokenSetting setting)
    {
        this.timeProvider = timeProvider;
        this.cache = cache;
        this.setting = setting;
    }

    // 署名を確かめる前に、端末を引くための Id (sub) を読む。形の崩れた値は null
    public static Guid? ReadDeviceId(string assertion)
    {
        if (!Handler.CanReadToken(assertion))
        {
            return null;
        }

        try
        {
            var token = Handler.ReadJsonWebToken(assertion);
            return Guid.TryParseExact(token.Subject, "D", out var id) ? id : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public async ValueTask<bool> ValidateAsync(string assertion, DeviceEntity device)
    {
        var deviceId = device.Id.ToString("D");
        var result = await Handler.ValidateTokenAsync(assertion, new TokenValidationParameters
        {
            ValidIssuer = deviceId,
            ValidAudience = setting.Issuer,
            IssuerSigningKey = new JsonWebKey(device.PublicKey),
            ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
            RequireExpirationTime = true,
            ClockSkew = ClockSkew
        });
        if (!result.IsValid || (result.SecurityToken is not JsonWebToken token) || (token.Subject != deviceId) || String.IsNullOrEmpty(token.Id))
        {
            return false;
        }

        // 期限の遠い要求は受けない (盗まれても使える時間を短くする)
        var expires = new DateTimeOffset(token.ValidTo, TimeSpan.Zero);
        if (expires > timeProvider.GetUtcNow() + TimeSpan.FromMinutes(setting.AssertionMaxMinutes) + ClockSkew)
        {
            return false;
        }

        // 同じ jti は期限まで受けない (サーバの中で覚えるので、サーバを複数にするときは共有のキャッシュにする)
        var key = $"assertion:{deviceId}:{token.Id}";
        lock (sync)
        {
            if (cache.TryGetValue(key, out _))
            {
                return false;
            }

            cache.Set(key, true, expires + ClockSkew);
        }

        return true;
    }
}
