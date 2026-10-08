namespace TableOrder.TableApp.Shell;

// MainPage と通知の受け手 (OrderEventReceiver) から表示中の画面へ送る知らせ
public enum ShellEvent
{
    // 端末の戻る
    Back,

    // 来店が開いた (ホール端末で開いた)
    VisitOpened,

    // 来店が変わった (ホール端末で人数を直した、会計を始めた・やめた)
    VisitUpdated,

    // 来店が閉じた (レジで払い終えた、スタッフが閉じた、テーブルで払い終えた)
    VisitClosed,

    // 来店がほかのテーブルに移った (ホール端末で移した。このテーブルは待受に戻す)
    VisitMoved,

    // 店舗が変わった (注文の一時停止、ラストオーダー)
    StoreUpdated,

    // 品切れが変わった (ホール端末・キッチン端末で売り切れにした、残りの数がなくなった)
    StockUpdated,

    // 起動からやり直す (端末を無効にされた、テナントを止められた、EMM が接続先を替えた、通知を追いかけられなくなった)
    Restart
}
