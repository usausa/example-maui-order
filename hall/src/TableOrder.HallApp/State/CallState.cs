namespace TableOrder.HallApp.State;

// 終わっていない呼び出し。起動のときに読み、呼び出しの通知と来店の移動・終了で読み直す
public sealed class CallState
{
    public IReadOnlyList<CallListResponseItem> Items { get; private set; } = [];

    // まだ誰も向かっていない呼び出しの数 (呼び出しのタブに出す)
    public int WaitingCount => Items.Count(static x => x.Status == CallStatus.Open);

    public void Update(CallListResponse calls)
    {
        Items = calls.Items;
    }
}
