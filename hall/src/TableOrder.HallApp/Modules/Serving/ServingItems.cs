namespace TableOrder.HallApp.Modules.Serving;

// テーブルごとの提供を待つ明細 (できあがりの古い順)
public sealed partial class ServingGroup : ObservableObject
{
    public Guid VisitId { get; }

    [ObservableProperty]
    public partial string TableText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial IReadOnlyList<ServingLine> Lines { get; set; } = [];

    // 明細が 2 つ以上ある (すべて提供したを出す)
    [ObservableProperty]
    public partial bool HasMany { get; set; }

    public ServingGroup(ServingListResponseItem item, DateTimeOffset now)
    {
        VisitId = item.VisitId;
        Update(item, now);
    }

    // 明細の中身は替わらないので、並びが替わったときだけ作り直す (テーブルは席の移動で替わる)
    public void Update(ServingListResponseItem item, DateTimeOffset now)
    {
        TableText = ViewHelper.Table(item.TableName);
        if (!Lines.Select(static x => x.LineId).SequenceEqual(item.Lines.Select(static x => x.LineId)))
        {
            Lines = item.Lines.Select(x => new ServingLine(x, now)).ToList();
            HasMany = Lines.Count > 1;
        }

        Tick(now);
    }

    public void Tick(DateTimeOffset now)
    {
        foreach (var line in Lines)
        {
            line.Tick(now);
        }
    }
}

// 提供を待つ明細。名前、オプション、数量と、できあがってからの時間を出す
public sealed partial class ServingLine : ObservableObject
{
    private readonly DateTimeOffset? readyAt;

    public Guid LineId { get; }

    public string Name { get; }

    public string OptionText { get; }

    public bool HasOptions => OptionText.Length > 0;

    public string QuantityText { get; }

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = string.Empty;

    public ServingLine(ServingListResponseLine line, DateTimeOffset now)
    {
        LineId = line.LineId;
        readyAt = line.ReadyAt;
        Name = ViewHelper.Text(line.Name);
        OptionText = String.Join(" / ", line.Options.Select(ViewHelper.Text));
        QuantityText = $"× {line.Quantity}";
        Tick(now);
    }

    public void Tick(DateTimeOffset now)
    {
        ElapsedText = readyAt is { } at ? ViewHelper.Elapsed(now - at) : string.Empty;
    }
}
