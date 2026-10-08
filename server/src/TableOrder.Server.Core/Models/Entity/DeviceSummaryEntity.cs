namespace TableOrder.Server.Core.Models.Entity;

// 端末の一覧の行 (端末と、置き場所のテーブルの名前、状態の報告)。状態の報告がまだなければ報告の列は null
public sealed class DeviceSummaryEntity
{
    public Guid Id { get; set; }

    public DeviceKind Kind { get; set; }

    public string Name { get; set; } = default!;

    public Guid? TableId { get; set; }

    public string? TableName { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public int Version { get; set; }

    public string? AppVersion { get; set; }

    public decimal? BatteryLevel { get; set; }

    public bool? IsCharging { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }
}
