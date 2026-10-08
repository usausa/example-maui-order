namespace TableOrder.Server.Core.Models.Entity;

// 明細で選んだオプション (名前と差額は注文したときのもの)
public sealed class OrderLineOptionEntity
{
    public Guid TenantId { get; set; }

    public Guid LineId { get; set; }

    public int SortOrder { get; set; }

    public Guid OptionGroupId { get; set; }

    public Guid OptionId { get; set; }

    public LocalizedText Name { get; set; } = default!;

    public decimal PriceDelta { get; set; }
}
