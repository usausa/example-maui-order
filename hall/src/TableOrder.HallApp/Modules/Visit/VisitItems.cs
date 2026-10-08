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
        Lines = order.Lines.Select(static x => new VisitLine(x)).ToList();
    }
}

// 来店の詳細の明細。調理と提供の段階を出す (できあがりは運ぶ品として目立たせ、提供と取消は控えめにする)
public sealed class VisitLine
{
    public string Name { get; }

    public string OptionText { get; }

    public bool HasOptions => OptionText.Length > 0;

    public string QuantityText { get; }

    public string StatusText { get; }

    public bool IsReady { get; }

    public bool IsDone { get; }

    public VisitLine(OrderListResponseLine line)
    {
        Name = ViewHelper.Text(line.Name);
        OptionText = String.Join(" / ", line.Options.Select(static x => ViewHelper.Text(x.Name)));
        QuantityText = $"× {line.Quantity}";
        StatusText = ViewHelper.Name(line.Status);
        IsReady = line.Status == OrderLineStatus.Ready;
        IsDone = line.Status is OrderLineStatus.Served or OrderLineStatus.Cancelled;
    }
}
