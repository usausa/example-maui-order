namespace TableOrder.KitchenApp.Components.Pages;

using System.Globalization;

// チケットの画面。受け持つ持ち場が 2 つ以上なら上に持ち場のタブ (すべてと持ち場ごと。まだ作り始めていないチケットの数を添える) を置く
// チケットを古い順に左から並べ、明細の作り始め・できあがりと、下げるを送る。下の帯から下げたチケット、品切れ、端末を開く
// 経過時間はしばらくごとに出し直し、通知で替わった一覧を出し直す
public sealed partial class TicketsPage : IDisposable
{
    // 経過時間を出し直す間隔
    private static readonly TimeSpan ClockInterval = TimeSpan.FromSeconds(15);

    // 画面を離れたら時計を止める
    private readonly CancellationTokenSource ticking = new();

    private bool isBusy;

    private string? message;

    private DateTimeOffset now;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required ILogger<TicketsPage> Log { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required TimeProvider TimeProvider { get; set; }

    [Inject]
    public required KioskScreen Kiosk { get; set; }

    [Inject]
    public required StoreState StoreState { get; set; }

    [Inject]
    public required TicketState TicketState { get; set; }

    [Inject]
    public required KitchenUsecase KitchenUsecase { get; set; }

    [Inject]
    public required KitchenEventReceiver Receiver { get; set; }

    private bool HasTabs => StoreState.Stations.Count > 1;

    private IEnumerable<KitchenTicketListResponseItem> Tickets =>
        TicketState.Open.Where(x => (TicketState.SelectedStationId is null) || (x.StationId == TicketState.SelectedStationId));

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    protected override void OnInitialized()
    {
        now = TimeProvider.GetUtcNow();
        Receiver.Changed += OnChanged;
        Kiosk.SoundReady += OnChanged;
        _ = TickAsync(ticking.Token);
    }

    public void Dispose()
    {
        Receiver.Changed -= OnChanged;
        Kiosk.SoundReady -= OnChanged;
        ticking.Cancel();
        ticking.Dispose();
    }

    private void OnChanged(object? sender, EventArgs e) => _ = InvokeAsync(Refresh);

    private async Task TickAsync(CancellationToken token)
    {
        using var timer = new PeriodicTimer(ClockInterval, TimeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(token))
            {
                await InvokeAsync(Refresh);
            }
        }
        catch (OperationCanceledException)
        {
            // 画面を離れた
        }
    }

    private void Refresh()
    {
        now = TimeProvider.GetUtcNow();
        StateHasChanged();
    }

    //--------------------------------------------------------------------------------
    // Station
    //--------------------------------------------------------------------------------

    private void Select(Guid? stationId) => TicketState.SelectedStationId = stationId;

    // まだ作り始めていない明細のあるチケットの数 (なければ空にしてバッジを出さない)
    private string Badge(Guid? stationId)
    {
        var count = TicketState.Open.Count(x => ((stationId is null) || (x.StationId == stationId)) && x.Lines.Any(static line => line.Status == OrderLineStatus.Ordered));
        return count > 0 ? count.ToString(CultureInfo.CurrentCulture) : string.Empty;
    }

    // すべての持ち場を並べるときは、チケットに持ち場の名前を添える
    private string? StationName(KitchenTicketListResponseItem ticket) =>
        HasTabs && (TicketState.SelectedStationId is null) ? StoreState.Stations.FirstOrDefault(x => x.Id == ticket.StationId)?.Name : null;

    //--------------------------------------------------------------------------------
    // Ticket
    //--------------------------------------------------------------------------------

    private async Task AdvanceAsync(KitchenTicketListResponseItem ticket, KitchenTicketListResponseLine line)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        try
        {
            Report(await KitchenUsecase.AdvanceLineAsync(ticket, line));
        }
        finally
        {
            isBusy = false;
        }
    }

    private async Task BumpAsync(KitchenTicketListResponseItem ticket)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        try
        {
            Report(await KitchenUsecase.BumpAsync(ticket));
        }
        finally
        {
            isBusy = false;
        }
    }

    // 失敗を知らせる。ほかの端末で先に進めていた (状態に合わない、見つからない) ときは、読み直した一覧を出すだけにする
    private void Report<T>(ApiResult<T> result)
    {
        now = TimeProvider.GetUtcNow();
        if (result.IsSuccess || result.ErrorCode is ErrorCodes.LineStatusInvalid or ErrorCodes.NotFound)
        {
            return;
        }

        Log.WarnApiFailed(nameof(KitchenUsecase), result.Status, result.ErrorCode);
        message = ViewHelper.ErrorMessage(result);
    }

    private void CloseMessage() => message = null;

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    private void OpenDone() => Navigation.NavigateTo("done");

    private void OpenStock() => Navigation.NavigateTo("stock");

    private void OpenDevice() => Navigation.NavigateTo("device");
}
