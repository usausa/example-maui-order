namespace TableOrder.ReceptionApp.Usecase;

// 受付。空席の読み直しと、人数を送って来店を開く (席はサーバが人数の入る空席から決める)
public sealed class ReceptionUsecase
{
    private readonly ReceptionState receptionState;

    private readonly IReceptionApi receptionApi;

    // 送れたかわからなかった受付 (同じ人数の次の送信で、同じ来店の id を送り直す。人数が替われば別のお客様として新しく開く)
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

    // 人数を送って来店を開く (サーバは同じ id の送り直しに、開いた来店を返す)
    public async ValueTask<ApiResult<VisitResponse>> OpenVisitAsync(int adults, int children)
    {
        var id = (pendingOpen is { } pending) && (pending.Adults == adults) && (pending.Children == children) ? pending.VisitId : Guid.CreateVersion7();
        var result = await receptionApi.OpenVisitAsync(new VisitCreateRequest { Id = id, Adults = adults, Children = children });
        pendingOpen = result.Status == ApiStatus.Unavailable ? (adults, children, id) : null;
        return result;
    }
}
