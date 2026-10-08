namespace TableOrder.Client;

// API の実装が読む端末の側の値。端末のアプリが設定に持ち、登録と接続先の変更で替わる
public interface IDeviceContext
{
    // 注文サーバの URL
    string ApiEndPoint { get; }

    // 登録で受け取った端末の id。登録していないか、今の接続先で登録していなければ null
    Guid? DeviceId { get; }

    // トークンの要求に署名する鍵
    IDeviceKey Key { get; }
}
