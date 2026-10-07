namespace TableOrder.Client;

// 端末の登録・トークン・状態の報告・設定。すべての種類の端末 (テーブル、ホール、受付) が使う窓口
// 実装は REST / モックを DI で替える。アクセストークンは窓口の中で持ち、画面には渡さない
public interface IDeviceApi
{
    // トークンの要求が断られた (端末の無効化、テナントの停止)。どのスレッドで出すかは実装による
    event EventHandler<DeviceDeniedEventArgs>? Denied;

    // POST /devices/pair (トークンを使わずに呼ぶ)
    ValueTask<ApiResult<DevicePairResponse>> PairAsync(DevicePairRequest request, CancellationToken cancel = default);

    // POST /devices/token。起動のときに取り直し、置き場所の変更とテナントの再開をトークンに反映する
    ValueTask<ApiResult<NoContent>> AuthenticateAsync(CancellationToken cancel = default);

    // POST /devices/me/heartbeat
    ValueTask<ApiResult<NoContent>> ReportStatusAsync(DeviceHeartbeatRequest request, CancellationToken cancel = default);

    // GET /devices/me/config
    ValueTask<ApiResult<DeviceConfigResponse>> GetConfigAsync(CancellationToken cancel = default);
}
