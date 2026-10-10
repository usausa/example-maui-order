namespace TableOrder.HallApp.Components;

using Android.App;
using Android.Content;

public sealed partial class CallWatch
{
    // 始められなくてもアプリは続ける (次に画面が前に出たときに始め直す)
    public partial void Start()
    {
        var context = Application.Context;
        try
        {
            using var intent = new Intent(context, typeof(CallWatchService));
            context.StartForegroundService(intent);
        }
        catch (Java.Lang.Exception ex)
        {
            log.WarnCallWatchFailed(ex);
        }
    }
}
