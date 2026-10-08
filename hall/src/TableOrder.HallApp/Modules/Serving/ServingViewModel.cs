namespace TableOrder.HallApp.Modules.Serving;

// 提供のタブ。中身はこれから作る (ヘッダとタブの帯は基底が持つ)
public sealed class ServingViewModel : TabViewModelBase
{
    public ServingViewModel(
        StoreState storeState,
        CallState callState,
        ServingState servingState)
        : base(ViewId.Serving, storeState, callState, servingState)
    {
    }
}
