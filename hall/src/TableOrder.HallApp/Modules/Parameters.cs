namespace TableOrder.HallApp.Modules;

#pragma warning disable CA1724
public static class Parameters
{
    private const string Tab = nameof(Tab);

    private const string VisitId = nameof(VisitId);

    // 端末の画面を開いたタブを渡す (閉じたらそのタブに戻る)
    public static NavigationParameter MakeTab(ViewId id) =>
        new NavigationParameter().SetValue(Tab, id);

    public static ViewId? GetTab(this INavigationParameter parameter) =>
        parameter.TryGetValue<ViewId>(Tab, out var id) ? id : null;

    // 来店の詳細に来店を渡す
    public static NavigationParameter MakeVisit(Guid visitId) =>
        new NavigationParameter().SetValue(VisitId, visitId);

    public static Guid? GetVisitId(this INavigationParameter parameter) =>
        parameter.TryGetValue<Guid>(VisitId, out var id) ? id : null;
}
#pragma warning restore CA1724
