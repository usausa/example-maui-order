namespace TableOrder.TableApp.Components;

// 端末の鍵 (P-256)。端末の中から取り出せない形で作り (Android は Keystore)、登録で公開鍵を送り、トークンの要求に署名する
// 鍵は登録し直しても使い回し、端末を無効にされたときに消す (次に使うときに作り直す)
public sealed partial class DeviceKey : IDeviceKey
{
    private const string Alias = "TableOrder.DeviceKey";

    private readonly Lock sync = new();

    public byte[] GetPublicKey()
    {
        lock (sync)
        {
            return ReadPublicKey();
        }
    }

    public byte[] Sign(byte[] data)
    {
        lock (sync)
        {
            return SignData(data);
        }
    }

    public void Delete()
    {
        lock (sync)
        {
            DeleteKey();
        }
    }

    private static partial byte[] ReadPublicKey();

    private static partial byte[] SignData(byte[] data);

    private static partial void DeleteKey();
}
