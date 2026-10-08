namespace TableOrder.Server.Web.Hubs;

using Microsoft.AspNetCore.SignalR;

using TableOrder.Contract.Events;
using TableOrder.Server.Web.Application.Context;

// 店舗の通知のハブ。端末はつないで受けるだけで、ハブのメソッドは呼ばない
// つないだ端末は、トークンの店舗の種類と置き場所 (テーブル、持ち場) のグループに入る
// 接続の応答はグループに入る前に返るので、入り終えたら店舗の今の通し番号を付けて ready を送る (端末はそのあとに抜けた通知を読めば取りこぼさない)
[Authorize(Policy = Policies.AnyDevice)]
public sealed class StoreHub : Hub
{
    private readonly ApplicationServiceContextProvider contextProvider;

    private readonly EventService eventService;

    public StoreHub(
        ApplicationServiceContextProvider contextProvider,
        EventService eventService)
    {
        this.contextProvider = contextProvider;
        this.eventService = eventService;
    }

    public override async Task OnConnectedAsync()
    {
        var context = HttpServiceContext.Create(Context.GetHttpContext()!);
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
}
