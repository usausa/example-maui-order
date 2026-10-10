namespace TableOrder.Server.Web.Components;

using Microsoft.AspNetCore.Components;

using TableOrder.Server.Web.Application.Context;

// 画面のイベントと引数の受け取りの間は、業務の処理の文脈を始めておく
public abstract class AppPageBase : ComponentBase, IHandleEvent
{
    [Inject]
    private BlazorServiceScope ServiceScope { get; set; } = default!;

    // 続けて押した (ダブルクリック) とみなす間隔。処理がすぐに終わる (待ちのない書き込み) と、2 回目の押下は終わったあとに届く
    private const long RepeatMilliseconds = 1000;

    // 前の処理を終えた時刻 (Environment.TickCount64)
    private long? completedAt;

    // 押したボタンの処理を待っている間 (ボタンを押せなくし、重ねて押した処理を断る。二重押しで 2 回発行しない)
    protected bool Busy { get; private set; }

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

    // ほかの部品の知らせ (店舗の選び直し、店舗の通知) で読み直す。画面のスレッドで文脈を始めて行い、
    // 失敗は画面の ErrorBoundary に渡す (知らせの中の例外は待つ人がいないので、渡さないと捨てられる)
    protected Task ReloadAsync(Func<Task> reload) =>
        InvokeAsync(async () =>
        {
            try
            {
                using (BeginServiceScope())
                {
                    await reload();
                }

                StateHasChanged();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await DispatchExceptionAsync(ex);
            }
        });

    // 発行や追加のように、重ねて押すと 2 回行う処理を 1 回だけ行う (処理中は Busy でボタンを押せなくし、終えた直後に続けて届いた押下も断る)
    protected async Task RunOnceAsync(Func<Task> action)
    {
        if (Busy || ((completedAt is { } at) && (Environment.TickCount64 - at < RepeatMilliseconds)))
        {
            return;
        }

        Busy = true;
        try
        {
            await action();
        }
        finally
        {
            Busy = false;
            completedAt = Environment.TickCount64;
        }
    }
}
