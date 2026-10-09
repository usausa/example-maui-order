namespace TableOrder.ReceptionApp.Shell;

// MainPage と通知の受け手 (OrderEventReceiver) から表示中の画面へ送る知らせ
public enum ShellEvent
{
    // 端末の戻る
    Back,

    // 空席を読み直した (来店の開始、移動、終了の通知)
    VacancyChanged,

    // 店舗が変わった (チェーンと店舗の設定の版、ラストオーダーの時刻)
    StoreUpdated,

    // 起動からやり直す (端末を無効にされた、テナントを止められた、管理画面で端末を替えた、EMM が接続先を替えた、通知を追いかけられなくなった)
    Restart
}
