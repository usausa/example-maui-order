namespace TableOrder.HallApp.Shell;

// MainPage と通知の受け手 (OrderEventReceiver) から表示中の画面へ送る知らせ
public enum ShellEvent
{
    // 端末の戻る
    Back,

    // 店舗が変わった (注文の一時停止)
    StoreChanged,

    // 品切れが変わった (ホール端末・キッチン端末で売り切れにした、残りの数がなくなった)
    StockChanged,

    // 席の一覧を読み直した (来店、注文、呼び出しの通知)
    TablesChanged,

    // 呼び出しの一覧を読み直した (呼び出しの通知、来店の移動と終了)
    CallsChanged,

    // 提供の一覧を読み直した (明細の通知、来店の移動と終了)
    ServingChanged,

    // 起動からやり直す (端末を無効にされた、テナントを止められた、管理画面で端末を替えた、EMM が接続先を替えた、通知を追いかけられなくなった)
    Restart
}
