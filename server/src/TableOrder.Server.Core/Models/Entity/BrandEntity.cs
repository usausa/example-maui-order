namespace TableOrder.Server.Core.Models.Entity;

// チェーンの設定 (テナントの行のうち、名前・ロゴ・色)
public sealed class BrandEntity
{
    public Guid Id { get; set; }

    public LocalizedText BrandName { get; set; } = default!;

    public string? LogoImageName { get; set; }

    // 替える色 (JSON)
    public string? Theme { get; set; }

    public int Version { get; set; }
}
