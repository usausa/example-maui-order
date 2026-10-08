namespace TableOrder.Server.Web;

using TableOrder.Client;

// テストの端末の設定。接続先はテストのサーバにし、登録で受け取った端末の id はテストが入れる
public sealed class TestDeviceContext : IDeviceContext
{
    public string ApiEndPoint { get; set; }

    public Guid? DeviceId { get; set; }

    public IDeviceKey Key { get; } = new TestDeviceKey();

    public TestDeviceContext(string apiEndPoint)
    {
        ApiEndPoint = apiEndPoint;
    }
}
