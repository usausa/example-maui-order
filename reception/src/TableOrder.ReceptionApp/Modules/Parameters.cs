namespace TableOrder.ReceptionApp.Modules;

using TableOrder.ReceptionApp.Modules.Guide;

#pragma warning disable CA1724
public static class Parameters
{
    private const string Guide = nameof(Guide);

    // 案内の画面に受付の結果 (決まったテーブルか満席と、人数) を渡す
    public static NavigationParameter MakeGuide(GuideResult result) =>
        new NavigationParameter().SetValue(Guide, result);

    public static GuideResult? GetGuide(this INavigationParameter parameter) =>
        parameter.TryGetValue<GuideResult>(Guide, out var result) ? result : null;
}
#pragma warning restore CA1724
