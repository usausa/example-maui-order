namespace TableOrder.Terminal.Table.Modules.Menu;

// 呼び出しの用件 (店舗の設定の表示順)
public sealed class CallReasonItem
{
    public string Code { get; }

    public string Name { get; }

    public string Glyph { get; }

    public CallReasonItem(string code, string name)
    {
        Code = code;
        Name = name;
        Glyph = ViewHelper.CallGlyph(code);
    }
}

// 用件のタイルの行
public sealed class CallReasonRow
{
    public IReadOnlyList<CallReasonItem> Items { get; }

    public CallReasonRow(IReadOnlyList<CallReasonItem> items)
    {
        Items = items;
    }
}

// 呼び出しの状態 (呼んでいる / 向かっている)
public sealed partial class CallStatusItem : ObservableObject
{
    public Guid Id { get; }

    public string Name { get; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsAcknowledged { get; set; }

    public CallStatusItem(Guid id, string name, CallStatus status)
    {
        Id = id;
        Name = name;
        Update(status);
    }

    public void Update(CallStatus status)
    {
        IsAcknowledged = status == CallStatus.Acknowledged;
        StatusText = IsAcknowledged ? AppResources.CallAcknowledged : AppResources.CallOpen;
    }
}
