namespace TableOrder.Client;

// テストの端末の設定。登録で受け取った端末の id はテストが入れる
public sealed class TestDeviceContext : IDeviceContext
{
    public string ApiEndPoint => string.Empty;

    public Guid? DeviceId { get; set; }

    public IDeviceKey Key { get; } = new TestDeviceKey();
}
