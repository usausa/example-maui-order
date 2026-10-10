namespace TableOrder.Server.Web.Hubs;

using Microsoft.AspNetCore.SignalR;

using TableOrder.Contract.Events;
using TableOrder.Server.Web.Application.Authentication;
using TableOrder.Server.Web.Application.Context;

// 店舗の通知のハブ。端末はつないで受けるだけで、ハブのメソッドは呼ばない
// つないだ端末は、トークンの店舗の種類と置き場所 (テーブル、持ち場) のグループに入る
// 接続の応答はグループに入る前に返るので、入り終えたら店舗の今の通し番号を付けて ready を送る (端末はそのあとに抜けた通知を読めば取りこぼさない)
// つないだ端末は、すぐに拒む一覧に入ったら切るように覚える
[Authorize(Policy = Policies.AnyDevice)]
public sealed class StoreHub : Hub
{
    private readonly ApplicationServiceContextProvider contextProvider;

    private readonly StoreHubConnections connections;

    private readonly RevocationList revocationList;

    private readonly EventService eventService;

    public StoreHub(
        ApplicationServiceContextProvider contextProvider,
        StoreHubConnections connections,
        RevocationList revocationList,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.connections = connections;
        this.revocationList = revocationList;
        this.eventService = eventService;
    }

    public override async Task OnConnectedAsync()
    {
        var context = HttpServiceContext.Create(Context.GetHttpContext()!);

        // 覚える前に読み直した一覧に入った端末は、読み直しのあとの切る処理に漏れるので、覚えたあとに一覧を引いて切る
        var tenantId = context.RequireTenantId();
        var deviceId = context.RequireDeviceId();
        connections.Add(Context, tenantId, deviceId);
        if (revocationList.IsRevoked(tenantId, deviceId))
        {
            Context.Abort();
            return;
        }

        // つなぐ途中で失敗すると (切れた、DB の失敗)、SignalR は切れた知らせ (OnDisconnectedAsync) を呼ばないので、覚えた接続をここで外す
        try
        {
            foreach (var group in StoreHubGroups.Of(context))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, group);
            }

            await base.OnConnectedAsync();

            // グループに入れ終えたあとの番号にする (これより後の通知は、すべてグループに届く)
            using var scope = contextProvider.Begin(() => context);
            var lastSeq = await eventService.GetLastSeqAsync(Context.ConnectionAborted);
            await Clients.Caller.SendAsync(HubMethods.Ready, lastSeq, Context.ConnectionAborted);
        }
        catch
        {
            connections.Remove(Context.ConnectionId);
            throw;
        }
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
