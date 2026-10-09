namespace TableOrder.KitchenApp.Browser;

using Microsoft.JSInterop;

// 店の端末として置くための部品 (wwwroot/js/kiosk.js)。画面を消さず、触れたら全画面にし、新しいチケットを音で知らせる
// ブラウザは画面に触れるまで音を出さないので、鳴らせるようになったら SoundReady で知らせる
public sealed class KioskScreen : IAsyncDisposable
{
    private readonly IJSRuntime js;

    private IJSInProcessObjectReference? module;

    private DotNetObjectReference<KioskScreen>? reference;

    public bool IsSoundReady { get; private set; }

    public event EventHandler? SoundReady;

    public KioskScreen(IJSRuntime js)
    {
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

        module = await js.InvokeAsync<IJSInProcessObjectReference>("import", "./js/kiosk.js");
        reference = DotNetObjectReference.Create(this);
        module.InvokeVoid("keepScreenOn");
        module.InvokeVoid("keepFullscreen");
        module.InvokeVoid("prepareSound", reference);
    }

    public void Chime() => module?.InvokeVoid("chime");

    [JSInvokable]
    public void OnSoundReady()
    {
        IsSoundReady = true;
        SoundReady?.Invoke(this, EventArgs.Empty);
    }
}
