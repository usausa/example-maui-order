namespace TableOrder.KitchenApp.Browser;

using Microsoft.JSInterop;

// 店の端末として置くための部品 (wwwroot/js/kiosk.js)。画面を消さず、触れたら全画面にし、新しいチケットを音で知らせる
// ブラウザは画面に触れるまで音を出さないので、鳴らせるようになったら SoundReady で知らせる
// 使えなくてもチケットは扱えるので、失敗はログに残して進める (読み込めなければ次の起動で読み込み直す)
public sealed class KioskScreen : IAsyncDisposable
{
    private readonly ILogger<KioskScreen> log;

    private readonly IJSRuntime js;

    private IJSInProcessObjectReference? module;

    private DotNetObjectReference<KioskScreen>? reference;

    public bool IsSoundReady { get; private set; }

    public event EventHandler? SoundReady;

    public KioskScreen(ILogger<KioskScreen> log, IJSRuntime js)
    {
        this.log = log;
        this.js = js;
    }

    public async ValueTask DisposeAsync()
    {
        if (module is not null)
        {
            await module.DisposeAsync();
        }

        reference?.Dispose();
    }

    // 画面を消さないようにし、全画面と音を鳴らす準備をする (起動のたびに呼んでも、はじめの 1 回だけ行う)
    public async ValueTask InitializeAsync()
    {
        if (module is not null)
        {
            return;
        }

        try
        {
            module = await js.InvokeAsync<IJSInProcessObjectReference>("import", "./js/kiosk.js");
        }
        catch (JSException ex)
        {
            log.WarnKioskScreenUnavailable(ex);
            return;
        }

        reference = DotNetObjectReference.Create(this);
        try
        {
            module.InvokeVoid("keepScreenOn");
            module.InvokeVoid("keepFullscreen");
            module.InvokeVoid("prepareSound", reference);
        }
        catch (JSException ex)
        {
            log.WarnKioskScreenUnavailable(ex);
        }
    }

    public void Chime()
    {
        try
        {
            module?.InvokeVoid("chime");
        }
        catch (JSException ex)
        {
            log.WarnKioskScreenUnavailable(ex);
        }
    }

    [JSInvokable]
    public void OnSoundReady()
    {
        IsSoundReady = true;
        SoundReady?.Invoke(this, EventArgs.Empty);
    }
}
