namespace TableOrder.HallApp.Modules.Serving;

// テーブルごとの提供を待つ明細 (できあがりの古い順)。払い終えて閉じた来店は会計済みの印を出す (同じテーブルの次のお客様の品と見分ける)
public sealed partial class ServingGroup : ObservableObject
{
    public Guid VisitId { get; }

    [ObservableProperty]
    public partial string TableText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsPaid { get; set; }

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

    // 並びが替わったときだけ明細を作り直し (テーブルは席の移動で替わる)、残る明細は数量とできあがりの時刻を替える (一部の取消は同じ明細の数量を減らす)
    public void Update(ServingListResponseItem item, DateTimeOffset now)
    {
        TableText = ViewHelper.Table(item.TableName);
        IsPaid = item.VisitStatus == VisitStatus.Closed;
        if (Lines.Select(static x => x.LineId).SequenceEqual(item.Lines.Select(static x => x.LineId)))
        {
            for (var i = 0; i < Lines.Count; i++)
            {
                Lines[i].Update(item.Lines[i]);
            }
        }
        else
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
    private DateTimeOffset? readyAt;

    public Guid LineId { get; }

    public string Name { get; }

    public string OptionText { get; }

    public bool HasOptions => OptionText.Length > 0;

    [ObservableProperty]
    public partial string QuantityText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = string.Empty;

    public ServingLine(ServingListResponseLine line, DateTimeOffset now)
    {
        LineId = line.LineId;
        Name = ViewHelper.Text(line.Name);
        OptionText = String.Join(" / ", line.Options.Select(ViewHelper.Text));
        Update(line);
        Tick(now);
    }

    // 品とオプションは明細ごとに替わらない。数量 (一部の取消) とできあがりの時刻 (作り直し) は替わる
    public void Update(ServingListResponseLine line)
    {
        readyAt = line.ReadyAt;
        QuantityText = $"× {line.Quantity}";
    }

    public void Tick(DateTimeOffset now)
    {
        ElapsedText = readyAt is { } at ? ViewHelper.Elapsed(now - at) : string.Empty;
    }
}
