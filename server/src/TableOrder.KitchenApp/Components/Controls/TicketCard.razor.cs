namespace TableOrder.KitchenApp.Components.Controls;

// チケットのカード。テーブル、何回目の注文、経過時間 (遅れの時間を過ぎたら注意の色)、明細、下げる (下げたチケットは戻す)
// まだ下げていないチケットは、明細を押すと作り始め、もう一度押すとできあがりに進める (下げたチケットの明細は押せない)
public sealed partial class TicketCard
{
    [Parameter]
    [EditorRequired]
    public KitchenTicketListResponseItem Ticket { get; set; } = default!;

    // 経過時間を数える今の時刻 (画面がしばらくごとに渡し直す)
    [Parameter]
    public DateTimeOffset Now { get; set; }

    // 注意の色にするまでの時間 (0 は色を替えない)
    [Parameter]
    public TimeSpan AlertTime { get; set; }

    // すべての持ち場を並べるときの持ち場の名前 (持ち場ごとに出すときは null)
    [Parameter]
    public string? StationName { get; set; }

    // 下げたチケット (戻すを出し、明細は押せない)
    [Parameter]
    public bool IsDone { get; set; }

    [Parameter]
    public bool IsBusy { get; set; }

    [Parameter]
    public EventCallback<KitchenTicketListResponseLine> OnLine { get; set; }

    [Parameter]
    public EventCallback OnAction { get; set; }

    private int ElapsedMinutes => Math.Max(0, (int)(Now - Ticket.CreatedAt).TotalMinutes);

    private bool IsAlert => !IsDone && (AlertTime > TimeSpan.Zero) && (Now - Ticket.CreatedAt >= AlertTime);

    private string ActionIcon => IsDone ? Icons.Undo : Icons.DoneAll;

    private string ActionText => IsDone ? AppResources.TicketRecall : AppResources.TicketBump;

    private bool CanAdvance(KitchenTicketListResponseLine line) =>
        !IsDone && !IsBusy && line.Status is OrderLineStatus.Ordered or OrderLineStatus.Cooking;

    private static string LineClass(OrderLineStatus status) =>
        status switch
        {
            OrderLineStatus.Cooking => "line-cooking",
            OrderLineStatus.Ready or OrderLineStatus.Served => "line-ready",
            OrderLineStatus.Cancelled => "line-cancelled",
            _ => "line-ordered"
        };

    private static string LineStatusText(OrderLineStatus status) =>
        status switch
        {
            OrderLineStatus.Cooking => AppResources.LineCooking,
            OrderLineStatus.Ready => AppResources.LineReady,
            OrderLineStatus.Served => AppResources.LineServed,
            OrderLineStatus.Cancelled => AppResources.LineCancelled,
            _ => string.Empty
        };
}
