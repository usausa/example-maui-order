namespace TableOrder.Client;

// 端末が使えなくなった理由 (トークンの要求が断られた)
public enum DeviceDenial
{
    // 管理画面で端末を無効にした (登録からやり直す)
    Revoked,

    // テナントの契約を止めた (間をおいて取り直す)
    TenantSuspended
}

public sealed class DeviceDeniedEventArgs : EventArgs
{
    // 断られた端末 (登録し直した後に届いた前の端末の知らせを見分ける)
    public Guid DeviceId { get; }

    public DeviceDenial Reason { get; }

    public DeviceDeniedEventArgs(Guid deviceId, DeviceDenial reason)
    {
        DeviceId = deviceId;
        Reason = reason;
    }
}
