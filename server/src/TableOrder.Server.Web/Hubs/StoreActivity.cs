namespace TableOrder.Server.Web.Hubs;

// 店舗の通知を送ったことを、サーバの中で店舗を見ている画面 (管理画面の店内の今) に知らせる
// 送り手はほかのサーバが書いた通知も送るので、どのサーバで開いた画面にも届く
public sealed class StoreActivity
{
    private readonly Lock sync = new();

    private readonly Dictionary<(Guid TenantId, Guid StoreId), List<Action>> watchers = [];

    // 店舗の通知を送るたびに changed を呼ぶ (送り手のスレッドから呼ぶので、画面は自分の文脈に移してから読み直す)。戻り値を破棄すると止める
    public IDisposable Watch(Guid tenantId, Guid storeId, Action changed)
    {
        var key = (tenantId, storeId);
        lock (sync)
        {
            if (!watchers.TryGetValue(key, out var list))
            {
                list = [];
                watchers[key] = list;
            }

            list.Add(changed);
        }

        return new Watcher(this, key, changed);
    }

    public void Notify(Guid tenantId, Guid storeId)
    {
        Action[] targets;
        lock (sync)
        {
            if (!watchers.TryGetValue((tenantId, storeId), out var list))
            {
                return;
            }

            targets = [.. list];
        }

        foreach (var target in targets)
        {
            target();
        }
    }

    private void Unwatch((Guid TenantId, Guid StoreId) key, Action changed)
    {
        lock (sync)
        {
            if (watchers.TryGetValue(key, out var list) && list.Remove(changed) && (list.Count == 0))
            {
                watchers.Remove(key);
            }
        }
    }

    private sealed class Watcher : IDisposable
    {
        private readonly StoreActivity owner;

        private readonly (Guid TenantId, Guid StoreId) key;

        private readonly Action changed;

        private int disposed;

        public Watcher(StoreActivity owner, (Guid TenantId, Guid StoreId) key, Action changed)
        {
            this.owner = owner;
            this.key = key;
            this.changed = changed;
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) == 0)
            {
                owner.Unwatch(key, changed);
            }
        }
    }
}
