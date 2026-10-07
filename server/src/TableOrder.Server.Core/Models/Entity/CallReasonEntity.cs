namespace TableOrder.Server.Core.Models.Entity;

public sealed class CallReasonEntity
{
    public Guid TenantId { get; set; }

    public Guid StoreId { get; set; }

    public string Code { get; set; } = default!;

    public LocalizedText Name { get; set; } = default!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }
}
