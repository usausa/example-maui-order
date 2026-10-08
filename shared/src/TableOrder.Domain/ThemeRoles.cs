namespace TableOrder.Domain;

using System.Diagnostics.CodeAnalysis;

// テーブル端末の色の役割のうち、チェーンの設定で替えられるもの (端末の Colors.xaml の Brand・Neutral・Status の節と同じ名前)
// System の役割 (起動・端末の設定・電卓) はチェーンの色に替えない
public static class ThemeRoles
{
    public static IReadOnlyList<string> Brand { get; } =
    [
        "PrimaryColor",
        "PrimaryPressedColor",
        "OnPrimaryColor",
        "PrimaryContainerColor",
        "OnPrimaryContainerColor",
        "SecondaryColor",
        "SecondaryPressedColor",
        "OnSecondaryColor"
    ];

    public static IReadOnlyList<string> Neutral { get; } =
    [
        "CanvasColor",
        "SurfaceColor",
        "SurfaceVariantColor",
        "OnSurfaceColor",
        "OnSurfaceVariantColor",
        "OutlineColor",
        "OutlineVariantColor",
        "DisabledColor",
        "OnDisabledColor",
        "ScrimColor",
        "OnScrimColor"
    ];

    public static IReadOnlyList<string> Status { get; } =
    [
        "ErrorColor",
        "OnErrorColor",
        "SuccessColor",
        "OnSuccessColor",
        "WarningColor",
        "OnWarningColor"
    ];

    public static IReadOnlyList<string> All { get; } = [.. Brand, .. Neutral, .. Status];

    public static bool IsRole(string name) =>
        All.Contains(name, StringComparer.Ordinal);

    // #RRGGBB か #AARRGGBB (16 進)
    public static bool IsColor([NotNullWhen(true)] string? value) =>
        (value is { Length: 7 or 9 }) && (value[0] == '#') && value.Skip(1).All(Char.IsAsciiHexDigit);
}
