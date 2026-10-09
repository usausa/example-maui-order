namespace TableOrder.Client;

// 受付機 (店の入口でお客様が人数を入れる端末) が使う注文サーバの API。端末の登録と設定は IDeviceApi、通知は IOrderEvents に置く
// 実装は通信の方式 (REST、gRPC) ごとに作る
public interface IReceptionApi
{
    //--------------------------------------------------------------------------------
    // Store
    //--------------------------------------------------------------------------------

    // GET /store (ラストオーダーの時刻)
    ValueTask<ApiResult<StoreResponse>> GetStoreAsync(CancellationToken cancel = default);

    // GET /tables (空席の有無は Vacant で絞る)
    ValueTask<ApiResult<TableListResponse>> GetTablesAsync(TableStatus? status = null, CancellationToken cancel = default);

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    // POST /visits (テーブルを送らず、サーバが人数の入る空席を決める。空席がなければ NO_VACANT_TABLE)
    ValueTask<ApiResult<VisitResponse>> OpenVisitAsync(VisitCreateRequest request, CancellationToken cancel = default);
}
