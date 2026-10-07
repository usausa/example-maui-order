namespace TableOrder.Domain;

// 文字列の長さと入力の桁数・範囲 (画面と通信で同じ値を使う)
public static class Length
{
    // テーブル番号 (電卓で入れる)
    public const int TableNoDigits = 3;

    // 来店の人数 (大人と子どもそれぞれ)
    public const int MaxGuests = 20;

    // スタッフの PIN (電卓で入れる)
    public const int StaffPinDigits = 4;

    // 端末の登録のペアリングコード (管理画面で出し、端末で入れる)
    public const int PairingCodeDigits = 6;

    // 端末が送る名前とアプリの版
    public const int DeviceName = 50;

    public const int AppVersion = 50;
}
