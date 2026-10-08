namespace TableOrder.Domain;

using System.Diagnostics.CodeAnalysis;

// 画像 (料理の写真、チェーンのロゴ) の名前。内容が変わると名前も変わる (内容のハッシュを入れる) ので、同じ名前の画像は替えない
// 名前はサーバの置き場と端末の保存先の経路にそのまま使うので、階層を作れない文字だけにする
public static class ImageNames
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".webp"];

    // 英小文字か数字で始め、英小文字・数字・ハイフン・下線・点だけを使う。拡張子は png / jpg / jpeg / webp
    public static bool IsValid([NotNullWhen(true)] string? name) =>
        (name is { Length: > 0 and <= Length.ImageName }) &&
        IsLowerAlphanumeric(name[0]) &&
        name.All(static c => IsLowerAlphanumeric(c) || c is '-' or '_' or '.') &&
        Extensions.Any(x => name.EndsWith(x, StringComparison.Ordinal));

    private static bool IsLowerAlphanumeric(char c) =>
        c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
