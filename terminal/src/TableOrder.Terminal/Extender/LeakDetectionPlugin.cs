#if DEBUG
namespace TableOrder.Terminal.Extender;

using Smart.Mvvm.Resolver;
using Smart.Navigation.Plugins;

// 閉じた画面 (View と ViewModel) が一定時間後に GC で回収されたかを確かめ、残っていればリークの疑いとしてログに出す (Debug だけ)。
// Entry にスペルチェックの対象の単語が入っていた画面は、Android のスペルチェッカーがしばらく参照を持つので 5 秒では残ることがある
public sealed class LeakDetectionPlugin : PluginBase
{
    private static readonly TimeSpan CheckDelay = TimeSpan.FromSeconds(5);

    private ILogger Logger => field ??= ResolveProvider.Default.GetRequiredService<ILogger<LeakDetectionPlugin>>();

    public override void OnClose(IPluginContext pluginContext, object view, object? target)
    {
        var references = new List<KeyValuePair<string, WeakReference>>(2)
        {
            new(view.GetType().Name, new WeakReference(view))
        };
        if (target is not null)
        {
            references.Add(new(target.GetType().Name, new WeakReference(target)));
        }

        Application.Current?.Dispatcher.DispatchDelayed(CheckDelay, () => Check(references));
    }

    private void Check(List<KeyValuePair<string, WeakReference>> references)
    {
        // Java 側からの参照 (プラットフォームのビュー) も切れるように両方の GC を回す
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Java.Lang.JavaSystem.Gc();
        GC.Collect();

        foreach (var (name, reference) in references)
        {
            if (reference.IsAlive)
            {
                Logger.WarnLeakSuspected(name);
            }
            else
            {
                Logger.DebugClosedObjectCollected(name);
            }
        }
    }
}
#endif
