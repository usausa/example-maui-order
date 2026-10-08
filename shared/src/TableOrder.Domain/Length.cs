namespace TableOrder.Domain;

// 文字列の長さと入力の桁数・範囲 (画面と通信で同じ値を使う)
public static class Length
{
    // 来店の人数 (大人と子どもそれぞれ)
    public const int MaxGuests = 20;

    // スタッフの PIN (電卓で入れる)
    public const int StaffPinDigits = 4;

    // チェーンの名前 (言語ごと)
    public const int BrandName = 30;

    // 端末の登録のペアリングコード (管理画面で出し、端末で入れる)
    public const int PairingCodeDigits = 6;

    // 端末が送る名前とアプリの版
    public const int DeviceName = 50;

    public const int AppVersion = 50;

    // スタッフの操作に付ける Id (スタッフの管理は扱わないので、送られた値をそのまま記録する)
    public const int StaffId = 50;

    // 明細の取消の理由
    public const int CancelReason = 100;

    // 呼び出しの用件のコード
    public const int CallReasonCode = 20;

    // 決済サービスの名前と取引番号、払えなかった理由
    public const int PaymentReference = 100;

    public const int PaymentFailureReason = 200;

    // 注文の一時停止の間にテーブル端末に出す文言 (言語ごと)
    public const int PausedMessage = 100;

    // 品切れの残りの数
    public const int MaxStockRemaining = 9999;

    // 画像 (料理の写真、チェーンのロゴ) の名前
    public const int ImageName = 128;
}
