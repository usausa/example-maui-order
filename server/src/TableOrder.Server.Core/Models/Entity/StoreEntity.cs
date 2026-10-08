namespace TableOrder.Server.Core.Models.Entity;

public sealed class StoreEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public string Code { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;

    public string TimeZone { get; set; } = default!;

    // 店舗の現地時刻の HH:mm
    public string OpenTime { get; set; } = default!;

    public string CloseTime { get; set; } = default!;

    public string? LastOrderTime { get; set; }

    public bool OrderingPaused { get; set; }

    public LocalizedText? PausedMessage { get; set; }

    public TaxRounding TaxRounding { get; set; }

    public int MaxQuantityPerLine { get; set; }

    public int MaxLinesPerOrder { get; set; }

    // 言語の一覧 (JSON の配列)
    public string Languages { get; set; } = default!;

    // 支払方法の一覧 (JSON の配列)
    public string PaymentMethods { get; set; } = default!;

    public bool ElectronicReceipt { get; set; }

    // 機能の有無 (JSON。ない項目は既定の値)
    public string Features { get; set; } = default!;

    // スタッフの PIN のハッシュ (JSON)
    public string StaffPinHash { get; set; } = default!;

    // チェーンと店舗の設定の版 (設定を替えるたびに上げる)
    public int SettingsVersion { get; set; }

    public Guid? MenuPublicationId { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }
}
