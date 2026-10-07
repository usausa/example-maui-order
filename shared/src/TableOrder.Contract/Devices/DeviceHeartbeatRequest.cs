namespace TableOrder.Contract.Devices;

// 端末の状態の報告 (1 分ごと)。電池を持たない端末は電池の値を省く
public sealed class DeviceHeartbeatRequest
{
    public string? AppVersion { get; set; }

    // 電池の残り (0〜1)
    public decimal? BatteryLevel { get; set; }

    public bool? IsCharging { get; set; }
}
