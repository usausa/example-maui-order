namespace TableOrder.Terminal;

using System.Reflection;

#pragma warning disable CA1724
public static class Extensions
{
    //--------------------------------------------------------------------------------
    // Resource
    //--------------------------------------------------------------------------------

    public static T FindResource<T>(this ResourceDictionary resource, string key) =>
        resource.TryGetValue(key, out var value) ? (T)value : default!;

    public static IEnumerable<(string Key, T Value)> EnumValues<T>(this ResourceDictionary resource)
    {
        if (resource is { } resources)
        {
            foreach (var key in resources.Keys)
            {
                if (resources[key] is T value)
                {
                    yield return (key, value);
                }
            }

            if (resources.MergedDictionaries is not null)
            {
                foreach (var dictionary in resources.MergedDictionaries)
                {
                    foreach (var key in dictionary.Keys)
                    {
                        if (resources[key] is T value)
                        {
                            yield return (key, value);
                        }
                    }
                }
            }
        }
    }

    public static IEnumerable<Type> UnderNamespaceTypes(this Assembly assembly, Type baseNamespaceType)
    {
        var ns = baseNamespaceType.Namespace!;
        return assembly.ExportedTypes.Where(x => x.Namespace?.StartsWith(ns, StringComparison.Ordinal) ?? false);
    }

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 遷移の途中 (画面の OnNavigatedToAsync など) には遷移できないので、遷移を終えてから行う
    // 終えるまでに別の画面に移っていたら (起動からやり直すなど) 行わない (閉じた画面から遷移しない)

    // 画面 (source) の処理から移る。読み込みと通知での読み直しが重なっても、移るのは表示中の画面からの 1 回だけにする
    // ReSharper disable once AsyncVoidMethod
    public static async ValueTask PostForwardAsync(this INavigator navigator, object source, object viewId, NavigationParameter? parameter = null)
    {
        if (!ReferenceEquals(navigator.CurrentTarget, source))
        {
            return;
        }

        if (navigator.Executing)
        {
            // ReSharper disable once AsyncVoidEventHandlerMethod
            async void ExecutingChanged(object? sender, EventArgs args)
            {
                if (!navigator.Executing)
                {
                    navigator.ExecutingChanged -= ExecutingChanged;
                    if (ReferenceEquals(navigator.CurrentTarget, source))
                    {
                        await navigator.ForwardAsync(viewId, parameter);
                    }
                }
            }

            navigator.ExecutingChanged += ExecutingChanged;
        }
        else
        {
            await navigator.ForwardAsync(viewId, parameter);
        }
    }

    // ReSharper disable once AsyncVoidMethod
    public static async ValueTask PostActionAsync(this INavigator navigator, Func<Task> task)
    {
        if (navigator.Executing)
        {
            var target = navigator.CurrentTarget;

            // ReSharper disable once AsyncVoidEventHandlerMethod
            async void ExecutingChanged(object? sender, EventArgs args)
            {
                if (!navigator.Executing)
                {
                    navigator.ExecutingChanged -= ExecutingChanged;
                    if (ReferenceEquals(navigator.CurrentTarget, target))
                    {
                        await task();
                    }
                }
            }

            navigator.ExecutingChanged += ExecutingChanged;
        }
        else
        {
            await task();
        }
    }

    //--------------------------------------------------------------------------------
    // Reactive
    //--------------------------------------------------------------------------------

    public static IObservable<EventArgs> TickAsObservable(this IDispatcherTimer timer) =>
        Observable.FromEvent<EventHandler, EventArgs>(static h => (_, e) => h(e), h => timer.Tick += h, h => timer.Tick -= h);

    public static IObservable<ScreenStateEventArgs> StateChangedAsObservable(this IScreen screen) =>
        Observable.FromEvent<EventHandler<ScreenStateEventArgs>, ScreenStateEventArgs>(static h => (_, e) => h(e), h => screen.ScreenStateChanged += h, h => screen.ScreenStateChanged -= h);
}
#pragma warning restore CA1724
