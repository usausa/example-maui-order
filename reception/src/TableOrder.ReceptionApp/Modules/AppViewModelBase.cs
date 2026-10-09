namespace TableOrder.ReceptionApp.Modules;

using TableOrder.ReceptionApp.Shell;

[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public abstract class AppViewModelBase :
    ExtendViewModelBase,
    INavigatorAware,
    INavigationEventSupportAsync,
    INotifySupportAsync<ShellEvent>,
    INavigationLifecycleSupport
{
    public INavigator Navigator { get; set; } = default!;

    protected AppViewModelBase()
    {
        AcceptsCommand = false;
    }

    public virtual Task OnNavigatingFromAsync(INavigationContext context) => Task.CompletedTask;

    public virtual Task OnNavigatingToAsync(INavigationContext context) => Task.CompletedTask;

    public virtual Task OnNavigatedToAsync(INavigationContext context) => Task.CompletedTask;

    public void OnActivated() => AcceptsCommand = true;

    public void OnDeactivated() => AcceptsCommand = false;

    public async Task NavigatorNotifyAsync(ShellEvent parameter)
    {
        if (!AcceptsCommand)
        {
            return;
        }

        var task = parameter switch
        {
            ShellEvent.Back => OnNotifyBackAsync(),
            ShellEvent.VacancyChanged => OnVacancyChangedAsync(),
            ShellEvent.StoreUpdated => OnStoreUpdatedAsync(),
            ShellEvent.Restart => OnRestartAsync(),
            _ => Task.CompletedTask
        };
        await task.ConfigureAwait(true);
    }

    // 端末の戻る。お客様の画面ではアプリの外へ出さないように、画面ごとに扱いを決める
    protected abstract Task OnNotifyBackAsync();

    // サーバの通知。扱う画面だけが替える (受け手は状態を替えてから、操作の途中と遷移の間を待って知らせる)

    protected virtual Task OnVacancyChangedAsync() => Task.CompletedTask;

    protected virtual Task OnStoreUpdatedAsync() => Task.CompletedTask;

    // 起動からやり直す (起動で登録・トークン・店舗の設定を確かめ直す)。起動と端末の設定の画面は自分で確かめるので受けない
    protected virtual async Task OnRestartAsync() =>
        await Navigator.ForwardAsync(ViewId.Startup);
}
