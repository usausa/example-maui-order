namespace TableOrder.Contract.Events;

// device.updated の中身 (店舗のすべての端末に送り、その端末だけが起動からやり直す)
public sealed class DeviceUpdatedEventData
{
    public Guid DeviceId { get; set; }
}
