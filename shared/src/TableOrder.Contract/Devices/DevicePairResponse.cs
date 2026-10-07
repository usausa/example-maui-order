namespace TableOrder.Contract.Devices;

// 登録した端末。端末は Id をトークンの要求の署名 (sub) に使う
public sealed class DevicePairResponse
{
    public Guid DeviceId { get; set; }

    public DeviceKind Kind { get; set; }

    public Guid StoreId { get; set; }
}
