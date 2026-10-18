namespace TableOrder.Server.Core.Services;

using TableOrder.Contract.Devices;

// アクセストークンを出す。持つのは入口の側 (署名の鍵)。クレームは端末の記録から作った DeviceIdentity から入れる
public interface IAccessTokenIssuer
{
    DeviceTokenResponse CreateDeviceToken(DeviceIdentity identity);
}
