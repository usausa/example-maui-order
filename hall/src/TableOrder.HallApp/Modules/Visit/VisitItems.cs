namespace TableOrder.HallApp.Modules.Visit;

// 来店の詳細の 1 回分の注文 (例: 1 回目 18:10)
public sealed class VisitOrder
{
    public string Title { get; }

    public string TimeText { get; }

    public IReadOnlyList<VisitLine> Lines { get; }

    public VisitOrder(OrderListResponseItem order, string timeZone)
    {
        Title = ViewHelper.Format(AppResources.OrderTitleFormat, order.OrderNo);
        TimeText = ViewHelper.Time(order.OrderedAt, timeZone);
        Lines = order.Lines.Select(x => new VisitLine(order.Id, x)).ToList();
    }
}

// 来店の詳細の明細。調理と提供の段階を出す (できあがりは運ぶ品として目立たせ、提供と取消は控えめにする)
// 提供と取消を除く明細は、押すと取り消せる
public sealed class VisitLine
{
    public Guid OrderId { get; }

    public Guid LineId { get; }

    public string Name { get; }

    public string OptionText { get; }

    public bool HasOptions => OptionText.Length > 0;

    public int Quantity { get; }

    public string QuantityText { get; }

    public string StatusText { get; }

    public bool IsReady { get; }

    public bool IsDone { get; }

    public bool CanCancel => !IsDone;

    public VisitLine(Guid orderId, OrderListResponseLine line)
    {
        OrderId = orderId;
        LineId = line.Id;
        Name = ViewHelper.Text(line.Name);
        OptionText = String.Join(" / ", line.Options.Select(static x => ViewHelper.Text(x.Name)));
        Quantity = line.Quantity;
        QuantityText = $"× {line.Quantity}";
        StatusText = ViewHelper.Name(line.Status);
        IsReady = line.Status == OrderLineStatus.Ready;
        IsDone = line.Status is OrderLineStatus.Served or OrderLineStatus.Cancelled;
    }
}
