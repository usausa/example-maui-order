namespace TableOrder.Client.Rest;

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

// 前に読んだメニュー。版が変わっていなければ (304)、前に読んだものを使う (接続先と端末が同じときだけ)
// 端末の種類ごとの窓口がそれぞれ持つ
internal sealed class RestMenuCache
{
    private readonly IDeviceContext context;

    private readonly RestConnection connection;

    private volatile CachedMenu? menu;

    public RestMenuCache(IDeviceContext context, RestConnection connection)
    {
        this.context = context;
        this.connection = connection;
    }

    public async ValueTask<ApiResult<MenuResponse>> GetAsync(CancellationToken cancel)
    {
        var endPoint = context.ApiEndPoint;
        var deviceId = context.DeviceId;
        var cached = (menu is { } previous) && (previous.EndPoint == endPoint) && (previous.DeviceId == deviceId) ? previous : null;
        var result = await connection.SendWithTokenAsync(
            uri =>
            {
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                if (cached is not null)
                {
                    request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue($"\"{cached.Menu.MenuVersion}\""));
                }

                return request;
            },
            async (response, token) => (response.StatusCode == HttpStatusCode.NotModified) && (cached is not null)
                ? cached.Menu
                : await RestConnection.ReadAsync(response, ClientJsonContext.Default.MenuResponse, token),
            "menu",
            cancel);
        if ((result.Content is { } content) && (deviceId is { } id))
        {
            menu = new CachedMenu(endPoint, id, content);
        }

        return result;
    }

    private sealed record CachedMenu(string EndPoint, Guid DeviceId, MenuResponse Menu);
}
