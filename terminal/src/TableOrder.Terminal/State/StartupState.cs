namespace TableOrder.Terminal.State;

// アプリの起動の準備 (前回の異常終了の知らせなど) を終えたこと。MainPage はこれを待ってから起動の画面に進む
public sealed class StartupState
{
    private readonly TaskCompletionSource completedSource = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Completed => completedSource.Task;

    public void NotifyCompleted() => completedSource.TrySetResult();
}
