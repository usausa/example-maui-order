namespace TableOrder.TableApp.Modules;

public enum DialogId
{
    // 共通 (電卓、知らせ、確認は TableOrder.Terminal の TerminalDialogId)
    Language,

    // 待受
    GuestCount,

    // 注文
    ItemDetail,
    OrderConfirm,
    OrderHistory,
    StaffCall
}
