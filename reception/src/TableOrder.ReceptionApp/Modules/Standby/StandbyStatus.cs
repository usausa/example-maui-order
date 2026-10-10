namespace TableOrder.ReceptionApp.Modules.Standby;

// 待受の受付の状態 (受け付けられる、満席、受付を止めている、本日の受付の終了)
public enum StandbyStatus
{
    Available,
    Full,
    Stopped,
    Closed
}
