namespace TableOrder.KitchenApp.Components.Pages;

// 下げたチケット。直近に下げたチケットを新しい順に出し、戻す (押し間違い) を置く。戻したチケットはチケットの画面に戻る
public sealed partial class DonePage
{
    private bool isBusy;

    private string? message;

    private DateTimeOffset now;

    //--------------------------------------------------------------------------------
    // Property
    //--------------------------------------------------------------------------------

    [Inject]
    public required ILogger<DonePage> Log { get; set; }

    [Inject]
    public required NavigationManager Navigation { get; set; }

    [Inject]
    public required TimeProvider TimeProvider { get; set; }

    [Inject]
    public required StoreState StoreState { get; set; }

    [Inject]
    public required TicketState TicketState { get; set; }

    [Inject]
    public required KitchenUsecase KitchenUsecase { get; set; }

    //--------------------------------------------------------------------------------
    // Lifecycle
    //--------------------------------------------------------------------------------

    // 開いたときに読み直す (下げたチケットは通知では読み直さない)
    protected override async Task OnInitializedAsync()
    {
        now = TimeProvider.GetUtcNow();
        isBusy = true;
        try
        {
            Report(await KitchenUsecase.RefreshDoneAsync());
        }
        finally
        {
            isBusy = false;
        }
    }

    // 受け持つ持ち場が 2 つ以上なら、チケットに持ち場の名前を添える
    private string? StationName(KitchenTicketListResponseItem ticket) =>
        StoreState.Stations.Count > 1 ? StoreState.Stations.FirstOrDefault(x => x.Id == ticket.StationId)?.Name : null;

    //--------------------------------------------------------------------------------
    // Ticket
    //--------------------------------------------------------------------------------

    private async Task RecallAsync(KitchenTicketListResponseItem ticket)
    {
        if (isBusy)
        {
            return;
        }

        isBusy = true;
        try
        {
            Report(await KitchenUsecase.RecallAsync(ticket));
        }
        finally
        {
            isBusy = false;
        }
    }

    private void Report<T>(ApiResult<T> result)
    {
        now = TimeProvider.GetUtcNow();
        if (result.IsSuccess || (result.ErrorCode == ErrorCodes.NotFound))
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

    private void Close() => Navigation.NavigateTo("tickets", replace: true);
}
