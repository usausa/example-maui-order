namespace TableOrder.ReceptionApp.Usecase;

// 受付。空席の読み直しと、人数を送って来店を開く (席はサーバが人数の入る空席から決める)
public sealed class ReceptionUsecase
{
    private readonly ReceptionState receptionState;

    private readonly IReceptionApi receptionApi;

    // 送れたかわからなかった受付 (同じお客様が同じ人数で送り直すときに、同じ来店の id を送る。人数が替われば新しく開く)
    // お客様が替わるときは捨てる (前のお客様の来店を、次のお客様に案内しない)
    private (int Adults, int Children, Guid VisitId)? pendingOpen;

    public ReceptionUsecase(
        ReceptionState receptionState,
        IReceptionApi receptionApi)
    {
        this.receptionState = receptionState;
        this.receptionApi = receptionApi;
    }

    // 空席を読み直す
    public async ValueTask<ApiResult<TableListResponse>> RefreshVacancyAsync()
    {
        var result = await receptionApi.GetTablesAsync(TableStatus.Vacant);
        if (result.Content is { } tables)
        {
            receptionState.UpdateVacancy(tables);
        }

        return result;
    }

    // 人数を送って来店を開く (サーバは同じ id の送り直しに、開いた来店を状態を問わず返す)
    // 送り直しで返った来店が開いていなければ (送れていた来店をスタッフが閉じたなど)、使わずに新しく開く
    public async ValueTask<ApiResult<VisitResponse>> OpenVisitAsync(int adults, int children)
    {
        if ((pendingOpen is { } pending) && (pending.Adults == adults) && (pending.Children == children))
        {
            var resent = await SendOpenAsync(pending.VisitId, adults, children);
            if (resent.Content?.Status is null or VisitStatus.Open)
            {
                return resent;
            }
        }

        return await SendOpenAsync(Guid.CreateVersion7(), adults, children);
    }

    // お客様が替わった (人数の画面に入った)。送れたかわからなかった受付を捨てる
    public void ResetPendingOpen() => pendingOpen = null;

    private async ValueTask<ApiResult<VisitResponse>> SendOpenAsync(Guid id, int adults, int children)
    {
        var result = await receptionApi.OpenVisitAsync(new VisitCreateRequest { Id = id, Adults = adults, Children = children });
        pendingOpen = result.Status == ApiStatus.Unavailable ? (adults, children, id) : null;
        return result;
    }
}
