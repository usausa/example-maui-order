namespace TableOrder.Contract.Devices;

// アクセストークンの要求。端末の鍵で署名した使い捨ての JWT を送る
public sealed class DeviceTokenRequest
{
    public string Assertion { get; set; } = default!;
}
