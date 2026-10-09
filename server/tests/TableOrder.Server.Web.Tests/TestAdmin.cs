namespace TableOrder.Server.Web;

using System.Text.RegularExpressions;

using Microsoft.AspNetCore.Mvc.Testing;

// テストの管理画面の利用者 (ブラウザの代わり)。Cookie を持ち、移る先を確かめられるように自動では移らない
// 画面のフォームは、画面を読んで Blazor のフォームの名前と偽造防止の値を付けて送る
public sealed partial class TestAdmin : IDisposable
{
    public HttpClient Client { get; }

    public TestAdmin(ServerFactory factory)
    {
        Client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
    }

    public void Dispose() => Client.Dispose();

    public Task<HttpResponseMessage> SignInAsync(string email, string password) =>
        PostFormAsync("/account/sign-in", "sign-in", new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password
        });

    public Task<HttpResponseMessage> GetAsync(string path) =>
        Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

    // 画面 (path) を読んで、その画面のフォーム (formName。Blazor のフォームでなければ空) を送る (送り先が画面と違うときは action)
    public async Task<HttpResponseMessage> PostFormAsync(string path, string formName, IReadOnlyDictionary<string, string> fields, string? action = null)
    {
        var page = await Client.GetStringAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        var token = TokenPattern().Match(page).Groups[1].Value;
        var values = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token)
        };
        if (formName.Length > 0)
        {
            values["_handler"] = formName;
        }

        using var content = new FormUrlEncodedContent(values);
        return await Client.PostAsync(new Uri(action ?? path, UriKind.Relative), content, TestContext.Current.CancellationToken);
    }

    // 移る先の経路とクエリ (画面の移動は絶対の URL、サインアウトは相対の URL で返る)
    public static string? LocationOf(HttpResponseMessage response) =>
        response.Headers.Location is { } uri ? (uri.IsAbsoluteUri ? uri.PathAndQuery : uri.OriginalString) : null;

    // 画面の文字 (Blazor は日本語を文字参照で出すので戻す)
    public static async Task<string> ReadPageAsync(HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    // 偽造防止の値を付けずに送る (ほかのサイトからの要求の代わり)
    public async Task<HttpResponseMessage> PostWithoutTokenAsync(string path)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>());
        return await Client.PostAsync(new Uri(path, UriKind.Relative), content, TestContext.Current.CancellationToken);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();
}
