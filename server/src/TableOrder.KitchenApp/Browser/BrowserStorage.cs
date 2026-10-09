namespace TableOrder.KitchenApp.Browser;

using Microsoft.JSInterop;

// ブラウザの localStorage。WebAssembly の中で動くので、JavaScript を同期で呼ぶ
public sealed class BrowserStorage
{
    private readonly IJSInProcessRuntime js;

    public BrowserStorage(IJSRuntime js)
    {
        this.js = (IJSInProcessRuntime)js;
    }

    public string? Get(string key) => js.Invoke<string?>("localStorage.getItem", key);

    public void Set(string key, string value) => js.InvokeVoid("localStorage.setItem", key, value);

    public void Remove(string key) => js.InvokeVoid("localStorage.removeItem", key);
}
