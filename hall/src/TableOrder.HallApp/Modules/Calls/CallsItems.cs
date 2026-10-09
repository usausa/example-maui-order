namespace TableOrder.HallApp.Modules.Calls;

// 呼び出しのカード。テーブル、用件、状態と、呼ばれてからの時間を出す (時間は一覧が時間ごとに出し直す)
public sealed partial class CallItem : ObservableObject
{
    private DateTimeOffset createdAt;

    public Guid Id { get; }

    [ObservableProperty]
    public partial string TableText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReasonText { get; set; } = string.Empty;

    // 向かっている (向かうを出さない)
    [ObservableProperty]
    public partial bool IsAcknowledged { get; set; }

    [ObservableProperty]
    public partial string ElapsedText { get; set; } = string.Empty;

    public CallItem(CallListResponseItem call, string reasonText, DateTimeOffset now)
    {
        Id = call.Id;
        Update(call, reasonText, now);
    }

    public void Update(CallListResponseItem call, string reasonText, DateTimeOffset now)
    {
        createdAt = call.CreatedAt;
        TableText = ViewHelper.Table(call.TableName);
        ReasonText = reasonText;
        IsAcknowledged = call.Status == CallStatus.Acknowledged;
        Tick(now);
    }

    public void Tick(DateTimeOffset now)
    {
        ElapsedText = ViewHelper.Elapsed(now - createdAt);
    }
}
