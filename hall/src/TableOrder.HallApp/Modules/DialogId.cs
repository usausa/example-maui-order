namespace TableOrder.HallApp.Modules;

public enum DialogId
{
    // 来店 (電卓、知らせ、確認は TableOrder.Terminal の TerminalDialogId)
    GuestCount,
    MoveTable,
    LineCancel,
    Bill,

    // 代わりの注文
    OrderItem,

    // 品切れ
    StockEdit
}
