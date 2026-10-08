namespace TableOrder.TableApp.Modules.Menu;

// 注文履歴の 1 回分の注文 (例: 1 回目 12:05)
public sealed class HistoryOrder
{
    public string Title { get; }

    public string TimeText { get; }

    public IReadOnlyList<HistoryLine> Lines { get; }

    public HistoryOrder(string title, string timeText, IReadOnlyList<HistoryLine> lines)
    {
        Title = title;
        TimeText = timeText;
        Lines = lines;
    }
}

// 注文履歴の明細。調理と提供の段階を印で示す
public sealed partial class HistoryLine : ObservableObject
{
    public Guid Id { get; }

    public string Name { get; }

    public string OptionText { get; }

    public bool HasOptions => OptionText.Length > 0;

    public string QuantityText { get; }

    public string AmountText { get; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    // 作っている
    [ObservableProperty]
    public partial bool IsCooking { get; set; }

    // できあがり、まもなく運ぶ
    [ObservableProperty]
    public partial bool IsReady { get; set; }

    // 運んだ、取り消した
    [ObservableProperty]
    public partial bool IsDone { get; set; }

    public HistoryLine(OrderListResponseLine line, Language language)
    {
        Id = line.Id;
        Name = line.Name.Get(language);
        OptionText = String.Join(" / ", line.Options.Select(x => x.Name.Get(language)));
        QuantityText = $"× {line.Quantity}";
        AmountText = ViewHelper.Price(line.Amount);
        Update(line.Status);
    }

    public void Update(OrderLineStatus status)
    {
        StatusText = ViewHelper.Name(status);
        IsCooking = status == OrderLineStatus.Cooking;
        IsReady = status == OrderLineStatus.Ready;
        IsDone = status is OrderLineStatus.Served or OrderLineStatus.Cancelled;
    }
}
