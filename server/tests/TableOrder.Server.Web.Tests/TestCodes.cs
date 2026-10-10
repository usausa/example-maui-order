namespace TableOrder.Server.Web;

using System.Collections.Concurrent;

// テストのペアリングコードの端末の種類 (登録の要求に、コードに合う端末の種類を入れる)
// テストのサーバのコードは、サーバをまたいで重ならないように出して足す (ServerFactory)。管理画面の Service で出したコードとトークンは、種類を渡して登録する
public static class TestCodes
{
    private static readonly ConcurrentDictionary<string, DeviceKind> Kinds = new(new Dictionary<string, DeviceKind>
    {
        [SampleData.DemoTableCode] = DeviceKind.Table,
        [SampleData.DemoHallCode] = DeviceKind.Hall,
        [SampleData.DemoKitchenCode] = DeviceKind.Kitchen,
        [SampleData.DemoReceptionCode] = DeviceKind.Reception,
        [SampleData.TestTableCode] = DeviceKind.Table
    });

    public static void Add(string code, DeviceKind kind) => Kinds[code] = kind;

    // 知らないコード (正しくないコードを試すテスト) はテーブル端末にする (サーバはコードを確かめてから種類を比べる)
    public static DeviceKind KindOf(string code) => Kinds.GetValueOrDefault(code, DeviceKind.Table);
}
