namespace TableOrder.HallApp.State;

// 提供を待つ明細 (できあがった明細をテーブルごとに)。起動のときに読み、明細の通知と来店の移動・終了で読み直す
public sealed class ServingState
{
    public IReadOnlyList<ServingListResponseItem> Items { get; private set; } = [];

    // できあがった明細の数 (提供のタブに出す)
    public int WaitingCount => Items.Sum(static x => x.Lines.Count);

    public void Update(ServingListResponse serving)
    {
        Items = serving.Items;
    }
}
