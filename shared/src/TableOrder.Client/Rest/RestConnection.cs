namespace TableOrder.Client.Rest;

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

// 注文サーバへの REST の要求の送り方。端末の種類ごとの窓口 (RestDeviceApi、RestTableApi、RestHallApi、RestReceptionApi、RestKitchenApi) と通知 (SignalROrderEvents) が使う
// アクセストークンは中で持ち、期限の前と 401 を受けたときに取り直す (送り直しは 1 回だけ)。トークンの要求が断られたら (無効化、テナントの停止) Denied で知らせる
// テナントの停止で断られたら、起動の取り直し (AuthenticateAsync) がトークンを受け取るまで、ほかの要求はトークンを求めずに同じ断りを返す
// (止めている間、通知のつなぎ直しと状態の報告が要求を送り続けないように。再開を確かめるのは起動の画面だけにする)
// 結果は例外を投げずに ApiResult で返す。接続先と端末は要求のたびに IDeviceContext から読む (端末の設定で替えても、作り直さずに次の要求から使う)
public sealed class RestConnection : IDisposable
{
    private const string ApiPath = "api/v1/";

    // トークンを期限のどれだけ前に取り直すか (端末とサーバの時計のずれも見込む)
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(2);

    // 1 回の要求を待つ時間 (過ぎたら通信できないものとする)
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    private readonly IDeviceContext context;

    private readonly TimeProvider timeProvider;

    private readonly HttpClient client;

    private readonly SemaphoreSlim tokenLock = new(1, 1);

    // 今のアクセストークン (接続先と端末の組で持ち、どちらかが替わったら取り直す)
    private volatile AccessToken? currentToken;

    // テナントの停止で断られた接続先と端末 (起動の取り直しでトークンを受け取るまで、ほかの要求はトークンを求めない)
    private volatile Suspension? suspension;

    public event EventHandler<DeviceDeniedEventArgs>? Denied;

    public RestConnection(IDeviceContext context, OrderServerOptions options, TimeProvider timeProvider)
    {
        this.context = context;
        this.timeProvider = timeProvider;
        client = options.HandlerFactory is { } factory ? new HttpClient(factory()) : new HttpClient();
        client.Timeout = Timeout.InfiniteTimeSpan;
    }

    public void Dispose()
    {
        client.Dispose();
        tokenLock.Dispose();
    }

    //--------------------------------------------------------------------------------
    // Token
    //--------------------------------------------------------------------------------

    // アクセストークン (通知のハブにつなぐときにも使う)。期限が近いか、使えなかったトークン (rejected) か、force なら取り直す
    // テナントの停止で断られたあとは、force (起動の取り直し) のほかはトークンを求めずに同じ断りを返す
    internal async ValueTask<ApiResult<string>> GetAccessTokenAsync(string? rejected, bool force, CancellationToken cancel)
    {
        if (context.DeviceId is not { } deviceId)
        {
            return ApiResult.Failure<string>(ApiStatus.Unauthorized);
        }

        var endPoint = context.ApiEndPoint;
        if (!force && (Usable(endPoint, deviceId, rejected) is { } current))
        {
            return ApiResult.Success(current.Value);
        }

        if (!force && IsSuspended(endPoint, deviceId))
        {
            return Suspended();
        }

        try
        {
            await tokenLock.WaitAsync(cancel);
        }
        catch (OperationCanceledException ex)
        {
            return ApiResult.Failure<string>(ApiStatus.Canceled, exception: ex);
        }

        ApiResult<string> result;
        DeviceDeniedEventArgs? denied = null;
        try
        {
            // 待つ間にほかの要求が取り直していれば、それを使う (取り直しをまとめる)。断られていれば、続けて求めない
            if (!force && (Usable(endPoint, deviceId, rejected) is { } renewed))
            {
                return ApiResult.Success(renewed.Value);
            }

            if (!force && IsSuspended(endPoint, deviceId))
            {
                return Suspended();
            }

            result = await RequestTokenAsync(endPoint, deviceId, cancel);
            if ((result.Status == ApiStatus.Unauthorized) && (result.ErrorCode is ErrorCodes.DeviceRevoked or ErrorCodes.TenantSuspended))
            {
                denied = new DeviceDeniedEventArgs(deviceId, result.ErrorCode == ErrorCodes.DeviceRevoked ? DeviceDenial.Revoked : DeviceDenial.TenantSuspended);
            }
        }
        finally
        {
            tokenLock.Release();
        }

        if (denied is not null)
        {
            Denied?.Invoke(this, denied);
        }

        return result;
    }

    // 端末の鍵で署名した要求でトークンを受け取る。無効にした端末と止めたテナントは、ほかの要求と同じく Unauthorized で返す
    private async ValueTask<ApiResult<string>> RequestTokenAsync(string endPoint, Guid deviceId, CancellationToken cancel)
    {
        var now = timeProvider.GetUtcNow();
        string assertion;
        try
        {
            assertion = await DeviceCredentials.CreateAssertionAsync(context.Key, deviceId, now);
        }
        catch (CryptographicException ex)
        {
            return ApiResult.Failure<string>(ApiStatus.Unavailable, exception: ex);
        }

        var result = await PostAnonymousAsync("devices/token", new DeviceTokenRequest { Assertion = assertion }, ClientJsonContext.Default.DeviceTokenRequest, ClientJsonContext.Default.DeviceTokenResponse, cancel);
        if (result.Content is { } response)
        {
            currentToken = new AccessToken(endPoint, deviceId, response.AccessToken, now + TimeSpan.FromSeconds(response.ExpiresIn));
            suspension = null;
            return ApiResult.Success(response.AccessToken);
        }

        currentToken = null;
        if (result.ErrorCode == ErrorCodes.TenantSuspended)
        {
            suspension = new Suspension(endPoint, deviceId);
        }

        return result.ErrorCode is ErrorCodes.DeviceRevoked or ErrorCodes.TenantSuspended
            ? ApiResult.Failure<string>(ApiStatus.Unauthorized, result.ErrorCode, result.Detail)
            : Failure<DeviceTokenResponse, string>(result);
    }

    private AccessToken? Usable(string endPoint, Guid deviceId, string? rejected) =>
        (currentToken is { } current) && (current.EndPoint == endPoint) && (current.DeviceId == deviceId) && (current.Value != rejected) &&
        (timeProvider.GetUtcNow() < current.ExpiresAt - RenewBefore)
            ? current
            : null;

    private bool IsSuspended(string endPoint, Guid deviceId) =>
        (suspension is { } suspended) && (suspended.EndPoint == endPoint) && (suspended.DeviceId == deviceId);

    // テナントの停止で断られたときと同じ結果 (要求は送らず、Denied も出さない)
    private static ApiResult<string> Suspended() =>
        ApiResult.Failure<string>(ApiStatus.Unauthorized, ErrorCodes.TenantSuspended);

    //--------------------------------------------------------------------------------
    // Send
    //--------------------------------------------------------------------------------

    internal ValueTask<ApiResult<T>> GetAsync<T>(string path, JsonTypeInfo<T> resultType, CancellationToken cancel) =>
        SendWithTokenAsync(
            uri => new HttpRequestMessage(HttpMethod.Get, uri),
            (response, token) => ReadAsync(response, resultType, token),
            path,
            cancel);

    internal ValueTask<ApiResult<T>> PostAsync<TBody, T>(string path, TBody body, JsonTypeInfo<TBody> bodyType, JsonTypeInfo<T> resultType, CancellationToken cancel) =>
        SendWithTokenAsync(
            uri => new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body, bodyType) },
            (response, token) => ReadAsync(response, resultType, token),
            path,
            cancel);

    // 本文をそのまま受け取る (画像)
    internal ValueTask<ApiResult<byte[]>> GetBytesAsync(string path, CancellationToken cancel) =>
        SendWithTokenAsync(
            uri => new HttpRequestMessage(HttpMethod.Get, uri),
            static async (response, token) => await response.Content.ReadAsByteArrayAsync(token),
            path,
            cancel);

    // 本文のない操作 (やめる、取り消す)
    internal ValueTask<ApiResult<T>> PostEmptyAsync<T>(string path, JsonTypeInfo<T> resultType, CancellationToken cancel) =>
        SendWithTokenAsync(
            uri => new HttpRequestMessage(HttpMethod.Post, uri),
            (response, token) => ReadAsync(response, resultType, token),
            path,
            cancel);

    // 一部を変える操作 (人数の変更)
    internal ValueTask<ApiResult<T>> PatchAsync<TBody, T>(string path, TBody body, JsonTypeInfo<TBody> bodyType, JsonTypeInfo<T> resultType, CancellationToken cancel) =>
        SendWithTokenAsync(
            uri => new HttpRequestMessage(HttpMethod.Patch, uri) { Content = JsonContent.Create(body, bodyType) },
            (response, token) => ReadAsync(response, resultType, token),
            path,
            cancel);

    // 応答のない操作 (204)
    internal ValueTask<ApiResult<NoContent>> PostNoContentAsync<TBody>(string path, TBody body, JsonTypeInfo<TBody> bodyType, CancellationToken cancel) =>
        SendWithTokenAsync(
            uri => new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body, bodyType) },
            static (_, _) => ValueTask.FromResult(NoContent.Value),
            path,
            cancel);

    // 本文も応答もない操作 (すべて戻す)
    internal ValueTask<ApiResult<NoContent>> PostEmptyNoContentAsync(string path, CancellationToken cancel) =>
        SendWithTokenAsync(
            uri => new HttpRequestMessage(HttpMethod.Post, uri),
            static (_, _) => ValueTask.FromResult(NoContent.Value),
            path,
            cancel);

    // 置き換える操作で、応答のないもの (204。品切れ、注文の一時停止)
    internal ValueTask<ApiResult<NoContent>> PutNoContentAsync<TBody>(string path, TBody body, JsonTypeInfo<TBody> bodyType, CancellationToken cancel) =>
        SendWithTokenAsync(
            uri => new HttpRequestMessage(HttpMethod.Put, uri) { Content = JsonContent.Create(body, bodyType) },
            static (_, _) => ValueTask.FromResult(NoContent.Value),
            path,
            cancel);

    // トークンを使わずに送る (登録とトークンの要求)
    internal ValueTask<ApiResult<T>> PostAnonymousAsync<TBody, T>(string path, TBody body, JsonTypeInfo<TBody> bodyType, JsonTypeInfo<T> resultType, CancellationToken cancel) =>
        SendAsync(
            uri => new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(body, bodyType) },
            (response, token) => ReadAsync(response, resultType, token),
            path,
            null,
            cancel);

    // トークンを付けて送る。401 を受けたら、トークンを取り直して 1 回だけ送り直す
    internal async ValueTask<ApiResult<T>> SendWithTokenAsync<T>(Func<Uri, HttpRequestMessage> create, Func<HttpResponseMessage, CancellationToken, ValueTask<T>> read, string path, CancellationToken cancel)
    {
        string? rejected = null;
        while (true)
        {
            var access = await GetAccessTokenAsync(rejected, false, cancel);
            if (access.Content is not { } value)
            {
                return Failure<string, T>(access);
            }

            var result = await SendAsync(create, read, path, value, cancel);
            if ((result.Status != ApiStatus.Unauthorized) || (rejected is not null))
            {
                return result;
            }

            rejected = value;
        }
    }

    // 状態の分類: 4xx は受け付けられなかった (errorCode と文言を読む)、401 は登録が無効、5xx・時間切れ・通信できないは送り直せば通る見込みがある
    private async ValueTask<ApiResult<T>> SendAsync<T>(Func<Uri, HttpRequestMessage> create, Func<HttpResponseMessage, CancellationToken, ValueTask<T>> read, string path, string? accessToken, CancellationToken cancel)
    {
        if (!TryCreateUri(context.ApiEndPoint, ApiPath + path, out var uri))
        {
            return ApiResult.Failure<T>(ApiStatus.Unavailable);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel);
        timeout.CancelAfter(RequestTimeout);
        try
        {
            using var request = create(uri);
            if (accessToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }

            using var response = await client.SendAsync(request, timeout.Token);
            if (response.IsSuccessStatusCode || (response.StatusCode == HttpStatusCode.NotModified))
            {
                return ApiResult.Success(await read(response, timeout.Token));
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ApiResult.Failure<T>(ApiStatus.Unauthorized);
            }

            if (((int)response.StatusCode >= 500) || (response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests))
            {
                return ApiResult.Failure<T>(ApiStatus.Unavailable);
            }

            var problem = await ReadProblemAsync(response, timeout.Token);
            return ApiResult.Failure<T>(ApiStatus.Rejected, problem?.ErrorCode, problem?.Title);
        }
        catch (OperationCanceledException ex) when (cancel.IsCancellationRequested)
        {
            return ApiResult.Failure<T>(ApiStatus.Canceled, exception: ex);
        }
        // Android の通信 (AndroidMessageHandler) は、時間切れで閉じた接続などで WebException を投げる
        catch (Exception ex) when (ex is HttpRequestException or WebException or OperationCanceledException or JsonException or IOException or NotSupportedException)
        {
            return ApiResult.Failure<T>(ApiStatus.Unavailable, exception: ex);
        }
    }

    internal static async ValueTask<T> ReadAsync<T>(HttpResponseMessage response, JsonTypeInfo<T> type, CancellationToken cancel) =>
        await response.Content.ReadFromJsonAsync(type, cancel) ?? throw new JsonException("The response has no content.");

    // 失敗の応答が Problem Details でなければ、errorCode のない失敗にする
    private static async ValueTask<ProblemResponse?> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancel)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync(ClientJsonContext.Default.ProblemResponse, cancel);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static ApiResult<TResult> Failure<TSource, TResult>(ApiResult<TSource> source) =>
        ApiResult.Failure<TResult>(source.Status, source.ErrorCode, source.Detail, source.Exception);

    // 接続先 (注文サーバの URL) の下の経路。http か https の URL でなければ通信できないものとする
    internal static bool TryCreateUri(string endPoint, string path, [NotNullWhen(true)] out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(endPoint.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var root) ||
            ((root.Scheme != Uri.UriSchemeHttp) && (root.Scheme != Uri.UriSchemeHttps)))
        {
            return false;
        }

        uri = new Uri(root, path);
        return true;
    }

    private sealed record AccessToken(string EndPoint, Guid DeviceId, string Value, DateTimeOffset ExpiresAt);

    private sealed record Suspension(string EndPoint, Guid DeviceId);
}
