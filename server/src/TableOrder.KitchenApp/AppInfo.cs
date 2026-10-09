namespace TableOrder.KitchenApp;

using System.Reflection;

// アプリの版 (登録と状態の報告で送り、端末の画面に出す)
public static class AppInfo
{
    public static string Version { get; } = ReadVersion();

    // ビルドの情報 (+ の後ろ) は省く
    private static string ReadVersion()
    {
        var version = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        var index = version.IndexOf('+', StringComparison.Ordinal);
        return index >= 0 ? version[..index] : version;
    }
}
