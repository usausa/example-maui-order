namespace TableOrder.HallApp.Modules.Seats;

// 席のタイル。テーブルと今の来店の要約を出す (経過時間は一覧が時間ごとに出し直す)
public sealed partial class SeatTile : ObservableObject
{
    private DateTimeOffset? openedAt;

    public Guid TableId { get; }

    public int Capacity { get; private set; }

    // 今の来店 (空きは null)
    public Guid? VisitId { get; private set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsOccupied { get; set; }

    [ObservableProperty]
    public partial bool IsPaying { get; set; }

    // 来店中は人数、空きは定員
    [ObservableProperty]
    public partial string SizeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = string.Empty;

    // まだ出していない品の数 (なければ空)
    [ObservableProperty]
    public partial string UnservedText { get; set; } = string.Empty;

    // 終わっていない呼び出しの数 (なければ空)
    [ObservableProperty]
    public partial string CallText { get; set; } = string.Empty;

    public SeatTile(TableListResponseItem table, DateTimeOffset now)
    {
        TableId = table.Id;
        Update(table, now);
    }

    public void Update(TableListResponseItem table, DateTimeOffset now)
    {
        Name = table.Name;
        Capacity = table.Capacity;

        var visit = table.Visit;
        VisitId = visit?.VisitId;
        openedAt = visit?.OpenedAt;
        StatusText = ViewHelper.SeatStatus(visit?.Status);
        IsOccupied = visit is not null;
        IsPaying = visit?.Status == VisitStatus.Paying;
        SizeText = visit is not null ? ViewHelper.Guests(visit.Adults, visit.Children) : ViewHelper.Format(AppResources.CapacityFormat, table.Capacity);
        UnservedText = visit is { UnservedCount: > 0 } ? ViewHelper.Format(AppResources.UnservedFormat, visit.UnservedCount) : string.Empty;
        CallText = visit is { OpenCallCount: > 0 } ? ViewHelper.Format(AppResources.CallsFormat, visit.OpenCallCount) : string.Empty;
        Tick(now);
    }

    public void Tick(DateTimeOffset now)
    {
        ElapsedText = openedAt is { } at ? ViewHelper.Elapsed(now - at) : string.Empty;
    }
}
