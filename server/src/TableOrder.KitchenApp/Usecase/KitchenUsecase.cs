namespace TableOrder.KitchenApp.Usecase;

using System.Security.Cryptography;

// キッチンの手順。端末の登録、チケットの読み直しと操作、品切れ
// 操作が通ったら替わった一覧を読み直す (通知でも読み直すが、操作した画面がすぐ替わるように)
public sealed class KitchenUsecase
{
    // 登録したコードがキッチン端末のものでなかった
    public const string KindMismatch = "DEVICE_KIND_MISMATCH";

    // このブラウザでは端末の鍵を作れなかった
    public const string KeyUnavailable = "DEVICE_KEY_UNAVAILABLE";

    // 管理画面で見分けられるように、端末の名前に付けるブラウザごとの値 (はじめて登録するときに作って保存する)
    private const string NameSuffixKey = "tableorder.nameSuffix";

    private readonly ILogger<KitchenUsecase> log;

    private readonly BrowserStorage storage;

    private readonly Settings settings;

    private readonly TicketState ticketState;

    private readonly StockState stockState;

    private readonly IDeviceApi deviceApi;

    private readonly IKitchenApi kitchenApi;

    public KitchenUsecase(
        ILogger<KitchenUsecase> log,
        BrowserStorage storage,
        Settings settings,
        TicketState ticketState,
        StockState stockState,
        IDeviceApi deviceApi,
        IKitchenApi kitchenApi)
    {
        this.log = log;
        this.storage = storage;
        this.settings = settings;
        this.ticketState = ticketState;
        this.stockState = stockState;
        this.deviceApi = deviceApi;
        this.kitchenApi = kitchenApi;
    }

    //--------------------------------------------------------------------------------
    // Device
    //--------------------------------------------------------------------------------

    // 管理画面で出したペアリングコードで登録する (受け持つ持ち場はコードで決まる)
    public async ValueTask<ApiResult<DevicePairResponse>> PairAsync(string pairingCode)
    {
        DevicePublicKey publicKey;
        try
        {
            publicKey = DeviceCredentials.CreatePublicKey(await settings.Key.GetPublicKeyAsync());
        }
        catch (CryptographicException ex)
        {
            log.WarnDeviceKeyUnavailable(ex);
            return ApiResult.Failure<DevicePairResponse>(ApiStatus.Rejected, KeyUnavailable);
        }

        var result = await deviceApi.PairAsync(new DevicePairRequest
        {
            PairingCode = pairingCode,
            PublicKey = publicKey,
            DeviceName = DeviceName(),
            AppVersion = AppInfo.Version
        });
        if (result.Content is not { } device)
        {
            log.WarnDeviceRegistrationFailed(result.Status, result.ErrorCode);
            return result;
        }

        if (device.Kind != DeviceKind.Kitchen)
        {
            log.WarnDeviceRegistrationFailed(ApiStatus.Rejected, KindMismatch);
            return ApiResult.Failure<DevicePairResponse>(ApiStatus.Rejected, KindMismatch);
        }

        settings.Register(device.DeviceId);
        log.InfoDeviceRegistered(device.DeviceId, device.StoreId);
        return result;
    }

    private string DeviceName()
    {
        var suffix = storage.Get(NameSuffixKey);
        if (String.IsNullOrEmpty(suffix))
        {
            suffix = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
            storage.Set(NameSuffixKey, suffix);
        }

        return $"Browser {suffix}";
    }

    //--------------------------------------------------------------------------------
    // Ticket
    //--------------------------------------------------------------------------------

    // まだ下げていないチケット (受け持つすべての持ち場) を読み直す
    public async ValueTask<ApiResult<KitchenTicketListResponse>> RefreshTicketsAsync()
    {
        var result = await kitchenApi.GetTicketsAsync();
        if (result.Content is { } tickets)
        {
            ticketState.UpdateOpen(tickets);
        }

        return result;
    }

    // 明細を次の段階に進める (作り始め、できあがり)。ほかの端末で先に進めていても、読み直した一覧を出す
    public async ValueTask<ApiResult<KitchenTicketListResponseItem>> AdvanceLineAsync(KitchenTicketListResponseItem ticket, KitchenTicketListResponseLine line)
    {
        var result = line.Status == OrderLineStatus.Ordered
            ? await kitchenApi.StartLineAsync(ticket.Id, line.LineId)
            : await kitchenApi.ReadyLineAsync(ticket.Id, line.LineId);
        await RefreshTicketsAsync();
        return result;
    }

    // 残りの明細をできあがりにして下げる
    public async ValueTask<ApiResult<KitchenTicketListResponseItem>> BumpAsync(KitchenTicketListResponseItem ticket)
    {
        var result = await kitchenApi.BumpAsync(ticket.Id);
        await RefreshTicketsAsync();
        return result;
    }

    // 下げたチケットを戻す (押し間違い)。まだ下げていない一覧と下げた一覧の両方を読み直す
    public async ValueTask<ApiResult<KitchenTicketListResponseItem>> RecallAsync(KitchenTicketListResponseItem ticket)
    {
        var result = await kitchenApi.RecallAsync(ticket.Id);
        await RefreshTicketsAsync();
        await RefreshDoneAsync();
        return result;
    }

    // 直近に下げたチケットを読み直す
    public async ValueTask<ApiResult<KitchenTicketListResponse>> RefreshDoneAsync()
    {
        var result = await kitchenApi.GetTicketsAsync(status: KitchenTicketStatus.Done);
        if (result.Content is { } tickets)
        {
            ticketState.UpdateDone(tickets);
        }

        return result;
    }

    //--------------------------------------------------------------------------------
    // Stock
    //--------------------------------------------------------------------------------

    // 品切れと残りの数を送り、読み直す (通知でも替わるが、操作した画面がすぐ替わるように)
    public async ValueTask<ApiResult<NoContent>> UpdateStockAsync(Guid targetId, StockTargetKind kind, StockStatus status, int? remaining)
    {
        var result = await kitchenApi.UpdateStockAsync(targetId, new StockUpdateRequest { TargetKind = kind, Status = status, Remaining = remaining });
        await RefreshStockAsync();
        return result;
    }

    public async ValueTask<ApiResult<StockResponse>> RefreshStockAsync()
    {
        var result = await kitchenApi.GetStockAsync();
        if (result.Content is { } stock)
        {
            stockState.Update(stock);
        }

        return result;
    }
}
