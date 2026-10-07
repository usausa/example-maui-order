namespace TableOrder.Terminal.Table.Extender;

using System.ComponentModel;

using CommunityToolkit.Maui.Views;

// 来店が閉じたときと起動からやり直すときに、開いているポップアップを閉じる (前のお客様の注文履歴などを残さないように)
// ポップアップの処理の途中 (注文の送信など) は待ち、終わってから閉じる。重なったポップアップは内側から閉じる (外側は内側の結果を待つ間 Busy)
// 閉じるのはそのポップアップだけにし、先に閉じていたら何もしない (ほかのポップアップを閉じないように)
public sealed class PopupClosePlugin : IPopupPlugin
{
    private readonly ILogger<PopupClosePlugin> log;

    private readonly IReactiveMessenger messenger;

    public PopupClosePlugin(
        ILogger<PopupClosePlugin> log,
        IReactiveMessenger messenger)
    {
        this.log = log;
        this.messenger = messenger;
    }

    public void Extend(ContentView view)
    {
        if (view is Popup popup)
        {
            PopupCloser.Watch(log, messenger, popup);
        }
    }

    private sealed class PopupCloser
    {
        private readonly ILogger log;

        private readonly Popup popup;

        private IDisposable? subscription;

        private IBusyState? busyState;

        private bool requested;

        private bool closing;

        private PopupCloser(ILogger log, Popup popup)
        {
            this.log = log;
            this.popup = popup;
        }

        // 閉じるまで知らせを受け、閉じたら受けるのをやめる
        public static void Watch(ILogger log, IReactiveMessenger messenger, Popup popup)
        {
            var closer = new PopupCloser(log, popup);
            closer.subscription = messenger.Observe<PopupCloseMessage>().Subscribe(_ => closer.Request());
            popup.Closed += closer.HandleClosed;
        }

        private void Request()
        {
            if (requested)
            {
                return;
            }

            requested = true;
            busyState = (popup.BindingContext as ViewModelBase)?.BusyState;
            if (busyState is not null)
            {
                busyState.PropertyChanged += HandleBusyStateChanged;
            }

            TryClose();
        }

        private void HandleBusyStateChanged(object? sender, PropertyChangedEventArgs e) => TryClose();

        // そのポップアップの処理の途中は閉じない (処理が終わって Busy が外れたときにもう一度確かめる)
        private void TryClose()
        {
            if (!requested || closing || (busyState?.IsBusy ?? false))
            {
                return;
            }

            closing = true;
            CloseAsync().ContinueWith(
                t => log.WarnPopupCloseFailed(t.Exception!),
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted,
                TaskScheduler.Default);
        }

        // 閉じる間は Busy にして、ポップアップのボタンを受け付けない
        private async Task CloseAsync()
        {
            using (busyState?.Begin())
            {
                try
                {
                    await popup.CloseAsync().ConfigureAwait(true);
                }
                catch (InvalidPopupOperationException ex)
                {
                    // 上に別のポップアップが残っているときは、それが閉じて処理が進んだときにもう一度確かめる
                    log.WarnPopupCloseFailed(ex);
                    closing = false;
                }
            }
        }

        private void HandleClosed(object? sender, EventArgs e)
        {
            popup.Closed -= HandleClosed;
            subscription?.Dispose();
            if (busyState is not null)
            {
                busyState.PropertyChanged -= HandleBusyStateChanged;
            }
        }
    }
}
