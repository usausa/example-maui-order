namespace TableOrder.KitchenApp.Components.Layout;

// 画面を切り替える入れ物。起動を終えていないのに途中の画面の URL を開いたとき (読み込み直したときなど) は、
// その画面を作らずに起動からやり直す (画面は起動で読む状態 (メニューなど) を前提にする)
public sealed partial class MainLayout
{
    // 起動を終える前に開ける画面 (起動、端末の設定)
    private static readonly string[] StartupPaths = [string.Empty, "setup"];

    private bool canShow;

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required StartupState StartupState { get; set; }

    protected override void OnParametersSet()
    {
        var path = Navigation.ToBaseRelativePath(Navigation.Uri).Split('?', '#')[0];
        canShow = StartupState.IsCompleted || StartupPaths.Contains(path);
        if (!canShow)
        {
            Navigation.NavigateTo(String.Empty, replace: true);
        }
    }
}
