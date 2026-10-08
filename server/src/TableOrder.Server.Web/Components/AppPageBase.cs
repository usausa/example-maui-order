namespace TableOrder.Server.Web.Components;

using Microsoft.AspNetCore.Components;

using TableOrder.Server.Web.Application.Context;

// 画面のイベントと引数の受け取りの間は、業務の処理の文脈を始めておく
public abstract class AppPageBase : ComponentBase, IHandleEvent
{
    [Inject]
    private BlazorServiceScope ServiceScope { get; set; } = default!;

    Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg) => HandleEventAsync(callback, arg);

    protected async Task HandleEventAsync(EventCallbackWorkItem callback, object? arg)
    {
        using var scope = ServiceScope.Begin();

        var task = callback.InvokeAsync(arg);
        var shouldAwait = task.Status != TaskStatus.RanToCompletion && task.Status != TaskStatus.Canceled;
        StateHasChanged();
        if (shouldAwait)
        {
            try
            {
                await task;
            }
            catch
            {
                if (task.IsCanceled)
                {
                    return;
                }

                throw;
            }

            StateHasChanged();
        }
    }

    public override async Task SetParametersAsync(ParameterView parameters)
    {
        using var scope = ServiceScope.Begin();
        await base.SetParametersAsync(parameters);
    }

    // 画面のイベントの外 (ほかの部品からの知らせ) で業務の処理を呼ぶときに、文脈を始める
    protected IDisposable BeginServiceScope() => ServiceScope.Begin();
}
