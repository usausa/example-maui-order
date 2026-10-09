namespace TableOrder.KitchenApp.State;

// 起動を終えたか (起動で状態を読み終えるまでは、起動と端末の設定のほかの画面を開かない)
public sealed class StartupState
{
    public bool IsCompleted { get; private set; }

    public void Complete() => IsCompleted = true;
}
