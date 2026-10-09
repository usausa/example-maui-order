namespace TableOrder.Server.Core.Models.Entity;

// テーブルの管理の一覧の行 (使わなくしたテーブルも出す。置いた端末の数と、来店中かを添える)
public sealed class TableSetupEntity
{
    public Guid Id { get; set; }

    public string Name { get; set; } = default!;

    public string? Area { get; set; }

    public int Capacity { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public int Version { get; set; }

    // 置いた (使っている) 端末の数
    public int DeviceCount { get; set; }

    // 開いている来店がある (来店中か会計中)
    public bool HasOpenVisit { get; set; }
}
