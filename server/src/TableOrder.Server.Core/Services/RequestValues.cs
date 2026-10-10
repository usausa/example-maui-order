namespace TableOrder.Server.Core.Services;

// 要求の値。JSON で項目を省くと、null にしないと宣言した項目にも null が入るので、一覧は空として読む
internal static class RequestValues
{
    public static IReadOnlyList<T> ListOf<T>(IReadOnlyList<T>? values) => values ?? [];

    // 一覧の要素の null (JSON の読み込みは、null にしないと宣言した要素にも null を通す)
    public static bool HasNull<T>(IEnumerable<T?> values)
        where T : class =>
        values.Any(static x => x is null);
}
