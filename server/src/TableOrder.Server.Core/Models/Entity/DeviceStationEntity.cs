namespace TableOrder.Server.Core.Models.Entity;

// キッチン端末が受け持つ持ち場
public sealed class DeviceStationEntity
{
    public Guid TenantId { get; set; }

    public Guid DeviceId { get; set; }

    public Guid StationId { get; set; }
}
