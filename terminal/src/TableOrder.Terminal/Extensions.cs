namespace TableOrder.Terminal;

using TableOrder.Terminal.Components;

#pragma warning disable CA1724
public static class Extensions
{
    //--------------------------------------------------------------------------------
    // Resource
    //--------------------------------------------------------------------------------

    public static T FindResource<T>(this ResourceDictionary resource, string key) =>
        resource.TryGetValue(key, out var value) ? (T)value : default!;

    //--------------------------------------------------------------------------------
    // Navigation
    //--------------------------------------------------------------------------------

    // 遷移の途中 (画面の OnNavigatedToAsync など) には遷移できないので、遷移を終えてから行う
    // 終えるまでに別の画面に移っていたら (起動からやり直すなど) 行わない (閉じた画面から遷移しない)
    // 後回しの処理の例外は、ほかの画面の処理と同じくアプリを落として起動し直させる (捕まえて続けると、起動の途中などで止まったままになる)
    // 呼んだ画面が表示中かは確かめない (待ったあとに呼ぶ画面は、表示中のままかを自分で確かめてから呼ぶ)

    // ReSharper disable once AsyncVoidMethod
    public static async ValueTask PostForwardAsync(this INavigator navigator, object viewId, NavigationParameter? parameter = null)
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

    public static IObservable<ScreenStateEventArgs> StateChangedAsObservable(this IScreen screen) =>
        Observable.FromEvent<EventHandler<ScreenStateEventArgs>, ScreenStateEventArgs>(static h => (_, e) => h(e), h => screen.ScreenStateChanged += h, h => screen.ScreenStateChanged -= h);

    public static IObservable<EventArgs> BatteryChangedAsObservable(this DeviceInformation deviceInformation) =>
        Observable.FromEvent<EventHandler, EventArgs>(static h => (_, e) => h(e), h => deviceInformation.BatteryChanged += h, h => deviceInformation.BatteryChanged -= h);

    public static IObservable<EventArgs> NetworkChangedAsObservable(this DeviceInformation deviceInformation) =>
        Observable.FromEvent<EventHandler, EventArgs>(static h => (_, e) => h(e), h => deviceInformation.NetworkChanged += h, h => deviceInformation.NetworkChanged -= h);
}
#pragma warning restore CA1724
