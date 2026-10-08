namespace TableOrder.Terminal.Resources.Styles;

// 決めた幅の面 (ポップアップ、起動と端末の設定の列) の幅。広い画面 (タブレット) では決めた幅のまま、狭い画面 (スマートフォン) では左右に余白を残して画面に収める
// 幅を決めた要素は親の幅を越えて測られ、狭い画面からはみ出すため
// 画面を作るたびに読む (画面の向きはアプリごとに固定している)
public static class TerminalSizes
{
    // 狭い画面で左右に残す余白
    private const double Gutter = 16;

    // 電卓
    public static double Keypad => Fit(440);

    // 知らせと確認
    public static double Dialog => Fit(640);

    // 起動の列 (ロゴ、進み具合、失敗のときの操作)
    public static double Startup => Fit(480);

    // 端末の設定の列
    public static double Setup => Fit(720);

    private static double Fit(double width)
    {
        var display = DeviceDisplay.Current.MainDisplayInfo;
        return Math.Min(width, (display.Width / display.Density) - (Gutter * 2));
    }
}
