namespace TableOrder.Terminal.Components;

// チェーンの色をテーマに入れる (お客様の画面の端末で使う)。替えるのは Brand・Neutral・Status の役割 (ThemeRoles) だけで、System の役割 (起動・端末の設定・電卓) は替えない
// 色はアプリの資源の一番上に置き、Colors.xaml の値 (既定) より先に引かせる。スタイルはこれらの色を DynamicResource で引くので、作ってある画面にも効く
// 前のチェーンの色を残さないように、設定にない役割は既定の色に戻す
public sealed class ThemeManager
{
    private readonly ILogger<ThemeManager> log;

    private readonly ResourceDictionary resources;

    public ThemeManager(
        ILogger<ThemeManager> log,
        ResourceDictionary resources)
    {
        this.log = log;
        this.resources = resources;
    }

    public void Apply(IEnumerable<DeviceConfigResponseThemeColor> theme)
    {
        // 既定の色は Colors.xaml の辞書から読む。Source で入れた辞書は ContainsKey では見つからない (中身は読み込んだ辞書にある) ので TryGetValue で引く
        var defaults = resources.MergedDictionaries.First(static x => x.TryGetValue(ThemeRoles.All[0], out _));
        var colors = ThemeRoles.All.ToDictionary(static x => x, defaults.FindResource<Color>);
        foreach (var item in theme)
        {
            // 読めない色と知らない役割は使わずに記録する
            if (ThemeRoles.IsRole(item.Role) && ThemeRoles.IsColor(item.Color) && Color.TryParse(item.Color, out var color))
            {
                colors[item.Role] = color;
            }
            else
            {
                log.WarnThemeColorIgnored(item.Role, item.Color);
            }
        }

        foreach (var (role, color) in colors)
        {
            resources[role] = color;
        }
    }
}
