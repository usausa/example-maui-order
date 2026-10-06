namespace TableOrder.Terminal.Table.Shell;

// 注文サーバの通知を受けて状態を替え、表示中の画面に知らせる (画面での扱いは各画面が決める)
// 操作の途中 (Busy) と遷移の間は待ち、終わってから届いた順に渡す (お客様の操作と重ならないように)
public sealed class OrderEventReceiver
{
    private readonly ILogger<OrderEventReceiver> log;

    private readonly INavigator navigator;

    private readonly IBusyState busyState;

    private readonly VisitState visitState;

    private readonly StoreState storeState;

    private readonly IOrderEvents events;

    private readonly OrderUsecase orderUsecase;

    private readonly Queue<OrderEvent> pending = new();

    private long lastSeq;

    private bool delivering;

    public OrderEventReceiver(
        ILogger<OrderEventReceiver> log,
        INavigator navigator,
        IBusyState busyState,
        VisitState visitState,
        StoreState storeState,
        IOrderEvents events,
        OrderUsecase orderUsecase)
    {
        this.log = log;
        this.navigator = navigator;
        this.busyState = busyState;
        this.visitState = visitState;
        this.storeState = storeState;
        this.events = events;
        this.orderUsecase = orderUsecase;
    }

    public void Start()
    {
        events.Received += HandleReceived;
        busyState.PropertyChanged += (_, _) => Deliver();
        navigator.ExecutingChanged += (_, _) => Deliver();
    }

    // 通知はどのスレッドからも届くので、画面のスレッドで受ける
    private void HandleReceived(object? sender, OrderEventArgs args) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var e = args.Event;

            // 同じ通知が 2 回届くことがあるので、seq で重複を捨てる
            if (e.Seq <= lastSeq)
            {
                return;
            }

            lastSeq = e.Seq;
            log.DebugEventReceived(e.GetType().Name, e.Seq, e.OccurredAt);
            pending.Enqueue(e);
            Deliver();
        });

    private void Deliver()
    {
        if (delivering)
        {
            return;
        }

        // 待たずに進めるが、例外はログに残す
        DeliverAsync().ContinueWith(
            t => log.WarnEventDeliveryFailed(t.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private async Task DeliverAsync()
    {
        delivering = true;
        try
        {
            while ((pending.Count > 0) && !busyState.IsBusy && !navigator.Executing)
            {
                await ApplyAsync(pending.Dequeue()).ConfigureAwait(true);
            }
        }
        finally
        {
            delivering = false;
        }
    }

    private async Task ApplyAsync(OrderEvent e)
    {
        switch (e)
        {
            case VisitOpenedEvent opened:
                // 待受で人数を入れて自分で開いた来店なら、開き直さない
                if (!visitState.IsOpen)
                {
                    orderUsecase.OpenVisit(opened.Visit);
                }

                await navigator.NotifyAsync(ShellEvent.VisitOpened).ConfigureAwait(true);
                break;
            case VisitClosedEvent closed:
                // 来店を終えるのは画面が行う (お会計はお礼を出してから終える)
                if (visitState.IsOpen && (visitState.Id == closed.Visit.Id))
                {
                    visitState.Update(closed.Visit);
                    await navigator.NotifyAsync(ShellEvent.VisitClosed).ConfigureAwait(true);
                }

                break;
            case StoreUpdatedEvent updated:
                storeState.Update(updated.Store);
                await navigator.NotifyAsync(ShellEvent.StoreUpdated).ConfigureAwait(true);
                break;
        }
    }
}
