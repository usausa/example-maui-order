namespace TableOrder.Client;

// トークンを受け取れた (断られていたなら、回復した)
public sealed class DeviceAuthorizedEventArgs : EventArgs
{
    // トークンを受け取った端末 (登録し直した後に届いた前の端末の知らせを見分ける)
    public Guid DeviceId { get; }

    public DeviceAuthorizedEventArgs(Guid deviceId)
    {
        DeviceId = deviceId;
    }
}
