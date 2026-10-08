namespace TableOrder.Terminal.Table.State;

// 注文する前の商品 (カート)。注文を送れたら空にし、来店をまたいで残さない
public sealed class CartState
{
    private readonly List<CartLine> lines = [];

    public IReadOnlyList<CartLine> Lines => lines;

    public int Count => lines.Sum(static x => x.Quantity);

    public decimal Total => lines.Sum(static x => x.Amount);

    // 送ったが結果のわからない注文の Id。内容を変えずに送り直すときは同じ Id を使う
    public Guid? PendingOrderId { get; set; }

    // 同じ商品・オプション・時機の行があれば数量を足す
    public void Add(ItemSelection selection, decimal unitPrice)
    {
        var index = lines.FindIndex(x => x.IsSameSelection(selection.ItemId, selection.OptionIds, selection.Timing));
        if (index >= 0)
        {
            lines[index] = lines[index] with { Quantity = lines[index].Quantity + selection.Quantity };
        }
        else
        {
            lines.Add(new CartLine(Guid.CreateVersion7(), selection.ItemId, selection.OptionIds, selection.Quantity, selection.Timing, unitPrice));
        }

        PendingOrderId = null;
    }

    // 入れると行が増えるか (同じ品・オプション・出す時機の行がなければ増える)
    public bool AddsLine(ItemSelection selection) =>
        !lines.Exists(x => x.IsSameSelection(selection.ItemId, selection.OptionIds, selection.Timing));

    // 行の内容を詳細で直したとき
    public void Replace(Guid lineId, ItemSelection selection, decimal unitPrice)
    {
        var index = lines.FindIndex(x => x.Id == lineId);
        if (index >= 0)
        {
            lines[index] = new CartLine(lineId, selection.ItemId, selection.OptionIds, selection.Quantity, selection.Timing, unitPrice);
            PendingOrderId = null;
        }
    }

    // 0 で行を消す
    public void SetQuantity(Guid lineId, int quantity)
    {
        var index = lines.FindIndex(x => x.Id == lineId);
        if (index < 0)
        {
            return;
        }

        if (quantity <= 0)
        {
            lines.RemoveAt(index);
        }
        else
        {
            lines[index] = lines[index] with { Quantity = quantity };
        }

        PendingOrderId = null;
    }

    public CartLine? Find(Guid lineId) => lines.Find(x => x.Id == lineId);

    public void Clear()
    {
        lines.Clear();
        PendingOrderId = null;
    }
}
