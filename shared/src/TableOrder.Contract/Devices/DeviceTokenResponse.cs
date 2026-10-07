namespace TableOrder.Contract.Devices;

public sealed class DeviceTokenResponse
{
    public string AccessToken { get; set; } = default!;

    // 有効な秒数 (端末は切れる前に取り直す)
    public int ExpiresIn { get; set; }
}
