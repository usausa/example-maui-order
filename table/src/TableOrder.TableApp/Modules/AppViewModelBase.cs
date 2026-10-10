namespace TableOrder.TableApp.Modules;

using System.ComponentModel.DataAnnotations;

using Smart.Mvvm.Resolver;

using TableOrder.TableApp.Shell;

[ObservableGeneratorOption(Reactive = true, ViewModel = true)]
public abstract class AppViewModelBase :
    ExtendViewModelBase,
    IValidatable,
    INavigatorAware,
    INavigationEventSupportAsync,
    INotifySupportAsync<ShellEvent>,
    INavigationLifecycleSupport
{
    private List<ValidationResult>? validationResults;

    private IAccessor? propertyAccessor;

    public INavigator Navigator { get; set; } = default!;

    protected AppViewModelBase()
    {
        AcceptsCommand = false;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        System.Diagnostics.Debug.WriteLine($"{GetType()} is Disposed");
    }

    public void Validate(string name)
    {
        propertyAccessor ??= AccessorProvider.FindAccessor(GetType());
        if (propertyAccessor is null)
        {
            throw new InvalidOperationException($"Accessor is not supported. type=[{GetType()}]");
        }

        var value = propertyAccessor.GetValue(this, name);
        var context = new ValidationContext(this, ResolveProvider.Default, null)
        {
            MemberName = name
        };
        validationResults ??= [];

        if (!Validator.TryValidateProperty(value, context, validationResults))
        {
            Errors.AddError(name, validationResults[0].ErrorMessage!);
        }

        validationResults.Clear();
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
            ShellEvent.VisitOpened => OnVisitOpenedAsync(),
            ShellEvent.VisitUpdated => OnVisitUpdatedAsync(),
            ShellEvent.VisitClosed => OnVisitClosedAsync(),
            ShellEvent.VisitMoved => OnVisitMovedAsync(),
            ShellEvent.StoreUpdated => OnStoreUpdatedAsync(),
            ShellEvent.StockUpdated => OnStockUpdatedAsync(),
            ShellEvent.OrderAccepted => OnOrderAcceptedAsync(),
            ShellEvent.Restart => OnRestartAsync(),
            _ => Task.CompletedTask
        };
        await task.ConfigureAwait(true);
    }

    // 端末の戻る。お客様の画面から外へ出さないように、画面ごとに扱いを決める
    protected abstract Task OnNotifyBackAsync();

    // サーバの通知。扱う画面だけが替える (受け手は操作の途中と遷移の間を待ってから知らせる)

    protected virtual Task OnVisitOpenedAsync() => Task.CompletedTask;

    protected virtual Task OnVisitUpdatedAsync() => Task.CompletedTask;

    protected virtual Task OnVisitClosedAsync() => Task.CompletedTask;

    protected virtual Task OnVisitMovedAsync() => Task.CompletedTask;

    protected virtual Task OnStoreUpdatedAsync() => Task.CompletedTask;

    protected virtual Task OnStockUpdatedAsync() => Task.CompletedTask;

    protected virtual Task OnOrderAcceptedAsync() => Task.CompletedTask;

    // 起動からやり直す (起動で登録・トークン・店舗の設定を確かめ直す)。起動と端末の設定の画面は自分で確かめるので受けない
    protected virtual async Task OnRestartAsync() =>
        await Navigator.ForwardAsync(ViewId.Startup);
}
