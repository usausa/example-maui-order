namespace TableOrder.Server.Core.Models.Entity;

// 運営者の画面のテナントの一覧の行 (使っている店舗の数を添える)
public sealed class TenantSummaryEntity
{
    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public string Name { get; set; } = default!;

    public LocalizedText BrandName { get; set; } = default!;

    public TenantStatus Status { get; set; }

    public DateTimeOffset? SuspendedAt { get; set; }

    public int StoreCount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public int Version { get; set; }
}
