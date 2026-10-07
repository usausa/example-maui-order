namespace TableOrder.Server.Web.Application.Authentication;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using TableOrder.Contract.Devices;

// アクセストークン (JWT) を出す。クレームは端末の記録から作った DeviceIdentity から入れる (端末が送った値は使わない)
public sealed class AccessTokenService
{
    private static readonly JsonWebTokenHandler Handler = new();

    private readonly TimeProvider timeProvider;

    private readonly TokenSetting setting;

    private readonly SigningKeyProvider keys;

    public AccessTokenService(
        TimeProvider timeProvider,
        TokenSetting setting,
        SigningKeyProvider keys)
    {
        this.timeProvider = timeProvider;
        this.setting = setting;
        this.keys = keys;
    }

    public DeviceTokenResponse CreateDeviceToken(DeviceIdentity identity)
    {
        var now = timeProvider.GetUtcNow();
        var lifetime = TimeSpan.FromMinutes(setting.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [ClaimNames.Subject] = identity.DeviceId.ToString("D"),
            [ClaimNames.TenantId] = identity.TenantId.ToString("D"),
            [ClaimNames.StoreId] = identity.StoreId.ToString("D"),
            [ClaimNames.DeviceKind] = identity.Kind.ToString()
        };
        if (identity.TableId is { } tableId)
        {
            claims[ClaimNames.TableId] = tableId.ToString("D");
        }

        if (identity.StationIds.Count > 0)
        {
            claims[ClaimNames.StationIds] = identity.StationIds.Select(static x => x.ToString("D")).ToArray();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = setting.Issuer,
            Audience = setting.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = (now + lifetime).UtcDateTime,
            Claims = claims,
            SigningCredentials = keys.Credentials
        };

        return new DeviceTokenResponse
        {
            AccessToken = Handler.CreateToken(descriptor),
            ExpiresIn = (int)lifetime.TotalSeconds
        };
    }
}
