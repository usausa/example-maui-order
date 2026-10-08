namespace TableOrder.TableApp.Modules;

#pragma warning disable CA1724
public static class Parameters
{
    private const string CategoryId = nameof(CategoryId);

    // 言語を切り替えて画面を作り直すときに、選んでいたカテゴリを渡す
    public static NavigationParameter MakeCategoryId(Guid id) =>
        new NavigationParameter().SetValue(CategoryId, id);

    public static Guid? GetCategoryId(this INavigationParameter parameter) =>
        parameter.TryGetValue<Guid>(CategoryId, out var id) ? id : null;
}
#pragma warning restore CA1724
