namespace TableOrder.Client.Rest;

// 全端末に共通の要求 (登録、トークン、状態の報告、端末の設定) の REST の窓口
public sealed class RestDeviceApi : IDeviceApi
{
    private readonly RestConnection connection;

    // トークンはどの要求でも送り方の中で取り直すので、断られた知らせは送り方のものを渡す
    public event EventHandler<DeviceDeniedEventArgs>? Denied
    {
        add => connection.Denied += value;
        remove => connection.Denied -= value;
    }

    public RestDeviceApi(RestConnection connection)
    {
        this.connection = connection;
    }

    // 登録はトークンを使わずに送る
    public ValueTask<ApiResult<DevicePairResponse>> PairAsync(DevicePairRequest request, CancellationToken cancel = default) =>
        connection.PostAnonymousAsync("devices/pair", request, ClientJsonContext.Default.DevicePairRequest, ClientJsonContext.Default.DevicePairResponse, cancel);

    // トークンを取り直す (起動のとき。置き場所の変更とテナントの再開をトークンに反映する)
    public async ValueTask<ApiResult<NoContent>> AuthenticateAsync(CancellationToken cancel = default)
    {
        var result = await connection.GetAccessTokenAsync(null, true, cancel);
        return result.IsSuccess ? ApiResult.Success(NoContent.Value) : RestConnection.Failure<string, NoContent>(result);
    }

    public ValueTask<ApiResult<NoContent>> ReportStatusAsync(DeviceHeartbeatRequest request, CancellationToken cancel = default) =>
        connection.PostNoContentAsync("devices/me/heartbeat", request, ClientJsonContext.Default.DeviceHeartbeatRequest, cancel);

    public ValueTask<ApiResult<DeviceConfigResponse>> GetConfigAsync(CancellationToken cancel = default) =>
        connection.GetAsync("devices/me/config", ClientJsonContext.Default.DeviceConfigResponse, cancel);

    public ValueTask<ApiResult<byte[]>> GetImageAsync(string name, CancellationToken cancel = default) =>
        connection.GetBytesAsync("images/" + Uri.EscapeDataString(name), cancel);
}
