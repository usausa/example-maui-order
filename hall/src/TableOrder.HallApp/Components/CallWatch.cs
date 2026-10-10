namespace TableOrder.HallApp.Components;

// 画面が消えている間も新しい呼び出しを知らせられるように、前景サービス (CallWatchService) でアプリを動かし続ける
// 前景サービスは画面を出している間にしか始められないので、画面が前に出るたびに始める (動いていれば何もしない)
public sealed partial class CallWatch
{
    private readonly ILogger<CallWatch> log;

    public CallWatch(ILogger<CallWatch> log)
    {
        this.log = log;
    }

    public partial void Start();
}
