namespace TableOrder.Server.Web.Application.Authentication;

// アクセストークンのクレームの名前 (認証のあとは、この値を要求の文脈として信用する)
public static class ClaimNames
{
    // 端末の Id (外部はクライアントの Id)
    public const string Subject = "sub";

    public const string TenantId = "tenant_id";

    public const string StoreId = "store_id";

    public const string DeviceKind = "device_kind";

    public const string TableId = "table_id";

    public const string StationIds = "station_ids";
}
