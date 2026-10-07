namespace TableOrder.Terminal.Hall.Modules.Home;

// 仮の画面 (枠)。画面を作るまで、アプリの名前と版だけを出す
public sealed class HomeViewModel : AppViewModelBase
{
    public string VersionText { get; }

    public HomeViewModel(IAppInfo appInfo)
    {
        VersionText = $"Version {appInfo.VersionString} ({appInfo.BuildString})";
    }

    // 端末の戻るでは何もしない (アプリの外へ出さない)
    protected override Task OnNotifyBackAsync() => Task.CompletedTask;
}
