namespace TableOrder.Server.Web.Endpoints;

public static class ApiRoutes
{
    // API の経路の根 (例外処理、HTTP ログ、認可の応答は、この下かどうかで分かれる)
    public const string Root = "/api";

    public const string Prefix = Root + "/v1";

    public const string Devices = Prefix + "/devices";

    public const string Store = Prefix + "/store";

    public const string Tables = Prefix + "/tables";

    public const string Menu = Prefix + "/menu";

    public const string Images = Prefix + "/images";

    public const string Stock = Prefix + "/stock";

    public const string Visits = Prefix + "/visits";

    public const string Orders = Prefix + "/orders";

    public const string Kitchen = Prefix + "/kitchen";

    public const string Serving = Prefix + "/serving";

    public const string Calls = Prefix + "/calls";

    public const string Payments = Prefix + "/payments";

    public const string Events = Prefix + "/events";

    // 通知のハブ (API の経路の外。WebSocket はヘッダを付けられないので、トークンをクエリでも受ける)
    public const string StoreHub = "/hubs/store";

    // 電子レシートの画面 (API の経路の外。お客様が QR から開く)
    public const string Receipts = "/receipts";
}
