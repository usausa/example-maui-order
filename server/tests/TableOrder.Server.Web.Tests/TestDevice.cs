namespace TableOrder.Server.Web;

using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using TableOrder.Contract.Devices;

// テストの端末。鍵を作ってペアリングコードで登録し、鍵で署名した要求でアクセストークンを受け取る
public sealed class TestDevice : IDisposable
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly JsonWebTokenHandler Handler = new();

    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    public HttpClient Client { get; }

    public Guid DeviceId { get; private set; }

    public string AccessToken => Client.DefaultRequestHeaders.Authorization?.Parameter ?? string.Empty;

    public TestDevice(HttpClient client)
    {
        Client = client;
    }

    public void Dispose()
    {
        key.Dispose();
        Client.Dispose();
    }

    public DevicePublicKey PublicKey
    {
        get
        {
            var parameters = key.ExportParameters(false);
            return new DevicePublicKey
            {
                Kty = "EC",
                Crv = "P-256",
                X = Base64UrlEncoder.Encode(parameters.Q.X),
                Y = Base64UrlEncoder.Encode(parameters.Q.Y)
            };
        }
    }

    public Task<HttpResponseMessage> PairAsync(string code) =>
        Client.PostAsJsonAsync("/api/v1/devices/pair", new DevicePairRequest
        {
            PairingCode = code,
            PublicKey = PublicKey,
            DeviceName = "test",
            AppVersion = "1.0.0"
        }, JsonOptions, TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> RequestTokenAsync(string assertion) =>
        Client.PostAsJsonAsync("/api/v1/devices/token", new DeviceTokenRequest { Assertion = assertion }, JsonOptions, TestContext.Current.CancellationToken);

    // トークンの要求 (iss と sub は端末の Id、aud はサーバの名前、5 分以内、使い捨ての jti)
    public string CreateAssertion(ECDsa? signer = null)
    {
        var now = DateTime.UtcNow;
        return Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = DeviceId.ToString("D"),
            Audience = "tableorder",
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(2),
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = DeviceId.ToString("D"),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString("N")
            },
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(signer ?? key), SecurityAlgorithms.EcdsaSha256)
        });
    }

    // 登録してトークンを受け取り、以後の要求にトークンを付ける
    public async Task<DevicePairResponse> SignInAsync(string code)
    {
        using var pair = await PairAsync(code);
        pair.EnsureSuccessStatusCode();
        var device = (await pair.Content.ReadFromJsonAsync<DevicePairResponse>(JsonOptions, TestContext.Current.CancellationToken))!;
        DeviceId = device.DeviceId;

        using var token = await RequestTokenAsync(CreateAssertion());
        token.EnsureSuccessStatusCode();
        var response = (await token.Content.ReadFromJsonAsync<DeviceTokenResponse>(JsonOptions, TestContext.Current.CancellationToken))!;
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", response.AccessToken);
        return device;
    }

    public async Task<T> GetAsync<T>(string path)
    {
        using var response = await Client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, TestContext.Current.CancellationToken))!;
    }

    public Task<HttpResponseMessage> PostAsync<T>(string path, T body) =>
        Client.PostAsJsonAsync(path, body, JsonOptions, TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> PutAsync<T>(string path, T body) =>
        Client.PutAsJsonAsync(path, body, JsonOptions, TestContext.Current.CancellationToken);

    public Task<HttpResponseMessage> PatchAsync<T>(string path, T body) =>
        Client.PatchAsJsonAsync(path, body, JsonOptions, TestContext.Current.CancellationToken);

    // 成功を確かめて本文を読む
    public static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(JsonOptions, TestContext.Current.CancellationToken))!;
    }

    // Problem Details の errorCode
    public static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.TryGetProperty("errorCode", out var value) ? value.GetString() : null;
    }
}
