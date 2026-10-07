namespace TableOrder.Terminal.Hall.Modules;

using TableOrder.Terminal.Hall.Shell;

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
            _ => Task.CompletedTask
        };
        await task.ConfigureAwait(true);
    }

    // 端末の戻る。画面ごとに扱いを決める
    protected abstract Task OnNotifyBackAsync();
}
