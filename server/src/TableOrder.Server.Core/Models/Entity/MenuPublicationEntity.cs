namespace TableOrder.Server.Core.Models.Entity;

// 本部が公開したメニュー (公開の内容をそのまま持つ)
public sealed class MenuPublicationEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public string MenuVersion { get; set; } = default!;

    // MenuResponse の形の JSON
    public string Content { get; set; } = default!;

    public DateTimeOffset PublishedAt { get; set; }

    public Guid? ApiClientId { get; set; }
}
