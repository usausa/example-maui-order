namespace TableOrder.HallApp.Modules.Calls;

// 呼び出しのタブ。中身はこれから作る (ヘッダとタブの帯は基底が持つ)
public sealed class CallsViewModel : TabViewModelBase
{
    public CallsViewModel(
        StoreState storeState,
        CallState callState,
        ServingState servingState)
        : base(ViewId.Calls, storeState, callState, servingState)
    {
    }
}
