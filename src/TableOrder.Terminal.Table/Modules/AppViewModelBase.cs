namespace TableOrder.Terminal.Table.Modules;

using System.ComponentModel.DataAnnotations;

using Smart.Mvvm.Resolver;

using TableOrder.Terminal.Table.Shell;

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
        if (AcceptsCommand && (parameter == ShellEvent.Back))
        {
            await OnNotifyBackAsync().ConfigureAwait(true);
        }
    }

    // 端末の戻る。お客様の画面から外へ出さないように、画面ごとに扱いを決める
    protected abstract Task OnNotifyBackAsync();
}
