namespace TableOrder.Terminal.Table.Components;

using Android.App;
using Android.Content;

using AndroidX.Core.Content;

public sealed partial class ManagedConfiguration
{
    private RestrictionsReceiver? receiver;

    public void Dispose()
    {
        if (receiver is null)
        {
            return;
        }

        Application.Context.UnregisterReceiver(receiver);
        receiver.Dispose();
        receiver = null;
    }

    private static partial RawValues ReadValues()
    {
        var manager = (RestrictionsManager?)Application.Context.GetSystemService(Context.RestrictionsService);
        using var bundle = manager?.ApplicationRestrictions;
        return new RawValues(bundle?.GetString(ApiEndPointKey), bundle?.GetString(StaffPinKey), bundle?.GetString(EnrollmentTokenKey));
    }

    // EMM が値を替えた知らせは、動いている間に登録した受け口にだけ届く (マニフェストに書いた受け口には届かない)
    private partial void StartWatch()
    {
        receiver = new RestrictionsReceiver(this);
        using var filter = new IntentFilter(Intent.ActionApplicationRestrictionsChanged);
        ContextCompat.RegisterReceiver(Application.Context, receiver, filter, ContextCompat.ReceiverNotExported);
    }

    private sealed class RestrictionsReceiver : BroadcastReceiver
    {
        private readonly ManagedConfiguration owner;

        public RestrictionsReceiver(ManagedConfiguration owner)
        {
            this.owner = owner;
        }

        public override void OnReceive(Context? context, Intent? intent) => owner.Load();
    }
}
