namespace TableOrder.Server.Core.Models.Entity;

// テーブル (席)
public sealed class DiningTableEntity
{
    public Guid TenantId { get; set; }

    public Guid Id { get; set; }

    public Guid StoreId { get; set; }

    public string Name { get; set; } = default!;

    public string? Area { get; set; }

    public int Capacity { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public int Version { get; set; }
}
