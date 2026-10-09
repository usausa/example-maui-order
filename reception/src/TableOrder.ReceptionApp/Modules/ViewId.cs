namespace TableOrder.ReceptionApp.Modules;

public enum ViewId
{
    // システムの画面
    Startup,
    Setup,
    Staff,

    // お客様の画面 (待受から人数を入れて席を決め、案内を出して待受に戻る)
    Standby,
    Guests,
    Guide
}
