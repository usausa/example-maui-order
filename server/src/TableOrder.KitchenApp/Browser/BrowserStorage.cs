namespace TableOrder.KitchenApp.Browser;

using Microsoft.JSInterop;

// ブラウザの localStorage。WebAssembly の中で動くので、JavaScript を同期で呼ぶ
// 読み書きできないブラウザ (保存を止めている) では登録を持てないので、例外にして止める
public sealed class BrowserStorage
{
    private readonly IJSInProcessRuntime js;

    public BrowserStorage(IJSRuntime js)
    {
        this.js = (IJSInProcessRuntime)js;
    }

    public string? Get(string key)
    {
        try
        {
            return js.Invoke<string?>("localStorage.getItem", key);
        }
        catch (JSException ex)
        {
            throw Unavailable(ex);
        }
    }

    public void Set(string key, string value)
    {
        try
        {
            js.InvokeVoid("localStorage.setItem", key, value);
        }
        catch (JSException ex)
        {
            throw Unavailable(ex);
        }
    }

    public void Remove(string key)
    {
        try
        {
            js.InvokeVoid("localStorage.removeItem", key);
        }
        catch (JSException ex)
        {
            throw Unavailable(ex);
        }
    }

    private static InvalidOperationException Unavailable(JSException ex) =>
        new("The browser storage is not available.", ex);
}
