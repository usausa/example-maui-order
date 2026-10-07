namespace TableOrder.Terminal.Table.Shell;

// MainPage と通知の受け手 (OrderEventReceiver) から表示中の画面へ送る知らせ
public enum ShellEvent
{
    // 端末の戻る
    Back,

    // 来店が開いた (ホール端末で開いた)
    VisitOpened,

    // 来店が閉じた (レジで払い終えた、スタッフが閉じた、テーブルで払い終えた)
    VisitClosed,

    // 店舗が変わった (注文の一時停止、ラストオーダー)
    StoreUpdated
}
