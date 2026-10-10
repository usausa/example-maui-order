namespace TableOrder.Terminal.Modules;

// 画面で共通の操作の時間 (XAML からは {x:Static terminal:AppGestures.Xxx} で使う)
public static class AppGestures
{
    // スタッフメニューの隠れた入口 (ブランドの印) の長押しの時間 (ミリ秒)。お客様が触れて開かないように長めにする
    public const int StaffLongPress = 3000;
}
