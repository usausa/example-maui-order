namespace TableOrder.HallApp.State;

// 席の一覧 (テーブルと今の来店の要約)。起動のときに読み、来店・注文・呼び出しの通知で読み直す
public sealed class TableState
{
    public IReadOnlyList<TableListResponseItem> Items { get; private set; } = [];

    public void Update(TableListResponse tables)
    {
        Items = tables.Items;
    }
}
