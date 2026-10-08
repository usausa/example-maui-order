namespace TableOrder.Server.Web;

// テストごとに作る店舗 (ServerFactory.CreateStoreAsync)。テーブルごとのテーブル端末のコードと、ホール・キッチン・受付のコード
public sealed record TestStore(
    Guid TenantId,
    Guid StoreId,
    IReadOnlyList<Guid> TableIds,
    IReadOnlyList<string> TableCodes,
    string HallCode,
    string KitchenCode,
    string ReceptionCode);
