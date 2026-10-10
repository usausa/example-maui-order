namespace TableOrder.TableApp.State;

// 注文する前の商品 (カート)。注文を送れたら空にし、来店をまたいで残さない
// 送ったが結果のわからない注文があるうちは直さない (画面が止める。直すと、届いていた注文と二重になるか、同じ Id の違う内容として断られる)
public sealed class CartState
{
    private readonly List<CartLine> lines = [];

    public IReadOnlyList<CartLine> Lines => lines;

    public int Count => lines.Sum(static x => x.Quantity);

    public decimal Total => lines.Sum(static x => x.Amount);

    // 送ったが結果のわからない注文の Id。届いたか断られたかがわかるまで、同じ内容を同じ Id で送り直す
    public Guid? PendingOrderId { get; set; }

    public bool HasPendingOrder => PendingOrderId is not null;

    // 同じ商品・オプション・時機の行があれば数量を足す
    public void Add(ItemSelection selection, decimal unitPrice)
    {
        var index = lines.FindIndex(x => IsSame(x, selection));
        if (index >= 0)
        {
            lines[index] = lines[index] with { Quantity = lines[index].Quantity + selection.Quantity };
        }
        else
        {
            lines.Add(new CartLine(Guid.CreateVersion7(), selection.ItemId, selection.OptionIds, selection.Quantity, selection.Timing, unitPrice));
        }
    }

    // 入れるとまとめる行 (同じ品・オプション・出す時機の行)。なければ行が増える
    public CartLine? FindSame(ItemSelection selection) => lines.Find(x => IsSame(x, selection));

    // 行の内容を詳細で直したとき
    public void Replace(Guid lineId, ItemSelection selection, decimal unitPrice)
    {
        var index = lines.FindIndex(x => x.Id == lineId);
        if (index >= 0)
        {
            lines[index] = new CartLine(lineId, selection.ItemId, selection.OptionIds, selection.Quantity, selection.Timing, unitPrice);
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
    }

    public CartLine? Find(Guid lineId) => lines.Find(x => x.Id == lineId);

    public void Clear()
    {
        lines.Clear();
        PendingOrderId = null;
    }

    private static bool IsSame(CartLine line, ItemSelection selection) =>
        line.IsSameSelection(selection.ItemId, selection.OptionIds, selection.Timing);
}
