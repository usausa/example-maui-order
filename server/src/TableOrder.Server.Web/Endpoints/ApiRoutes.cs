namespace TableOrder.Server.Web.Endpoints;

public static class ApiRoutes
{
    // API の経路の根 (例外処理、HTTP ログ、認可の応答は、この下かどうかで分かれる)
    public const string Root = "/api";

    public const string Prefix = Root + "/v1";

    public const string Devices = Prefix + "/devices";

    public const string Store = Prefix + "/store";

    public const string Menu = Prefix + "/menu";

    public const string Stock = Prefix + "/stock";
}
