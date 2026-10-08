namespace TableOrder.HallApp.Modules.Seats;

// 席のタブ。中身はこれから作る (ヘッダとタブの帯は基底が持つ)
public sealed class SeatsViewModel : TabViewModelBase
{
    public SeatsViewModel(
        StoreState storeState,
        CallState callState,
        ServingState servingState)
        : base(ViewId.Seats, storeState, callState, servingState)
    {
    }
}
