namespace TableOrder.Terminal.Extender;

// ポップアップを開いている間は、下の画面を非アクティブにする (遷移の間と同じく、下の画面のコマンドを受けない)
// ポップアップは重ねて開くので、最初のポップアップを開き始めたとき (出す前) に止め、すべて閉じたら戻す
public sealed class PopupActivationPlugin : IPopupPlugin
{
    private readonly INavigator navigator;

    private INavigationLifecycleSupport? target;

    private int depth;

    public PopupActivationPlugin(INavigator navigator)
    {
        this.navigator = navigator;
    }

    public void Extend(ContentView view)
    {
    }

    public void OnOpening(ContentView view)
    {
        depth++;
        if (depth == 1)
        {
            target = navigator.CurrentTarget as INavigationLifecycleSupport;
            target?.OnDeactivated();
        }
    }

    public void OnClosed(ContentView view)
    {
        depth--;
        if (depth == 0)
        {
            // 開いている間にほかの画面に移っていたら (起動からやり直すなど)、止めた画面は戻さない (移った先の画面は遷移で有効になっている)
            if ((target is not null) && ReferenceEquals(navigator.CurrentTarget, target))
            {
                target.OnActivated();
            }

            target = null;
        }
    }
}
