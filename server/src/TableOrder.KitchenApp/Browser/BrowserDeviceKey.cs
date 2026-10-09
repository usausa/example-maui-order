namespace TableOrder.KitchenApp.Browser;

using System.Security.Cryptography;

using Microsoft.JSInterop;

// 端末の鍵 (P-256)。ブラウザの WebCrypto で取り出せない鍵を作り、IndexedDB に持つ (wwwroot/js/device-key.js)
// WebCrypto の署名は r と s を並べた形なので、窓口の約束の DER に直して返す
// 起動で InitializeAsync で部品を読み込む (消す操作と鍵を作れるかは同期で呼べるように、読み込んだ部品を持っておく)
// 起動を経ずに開いた画面 (読み込み直した端末の設定) からも使えるように、非同期の操作は部品がなければ読み込む
public sealed class BrowserDeviceKey : IDeviceKey, IAsyncDisposable
{
    private readonly IJSRuntime js;

    private IJSInProcessObjectReference? module;

    private IJSInProcessObjectReference Module => module ?? throw new InvalidOperationException("Device key is not initialized.");

    public BrowserDeviceKey(IJSRuntime js)
    {
        this.js = js;
    }

    public ValueTask DisposeAsync() =>
        module?.DisposeAsync() ?? ValueTask.CompletedTask;

    public async ValueTask InitializeAsync() => await LoadAsync();

    // 鍵を作れるブラウザか (HTTPS か localhost で開いていて、WebCrypto と IndexedDB がある)
    public bool IsAvailable => Module.Invoke<bool>("isAvailable");

    public async ValueTask<byte[]> GetPublicKeyAsync()
    {
        try
        {
            return await (await LoadAsync()).InvokeAsync<byte[]>("getPublicKey");
        }
        catch (JSException ex)
        {
            throw new CryptographicException("The device key is not available.", ex);
        }
    }

    public async ValueTask<byte[]> SignAsync(byte[] data)
    {
        try
        {
            return DeviceCredentials.ToDerSignature(await (await LoadAsync()).InvokeAsync<byte[]>("sign", data));
        }
        catch (JSException ex)
        {
            throw new CryptographicException("The device key could not sign.", ex);
        }
    }

    public void Delete() => Module.InvokeVoid("remove");

    private async ValueTask<IJSInProcessObjectReference> LoadAsync() =>
        module ??= await js.InvokeAsync<IJSInProcessObjectReference>("import", "./js/device-key.js");
}
