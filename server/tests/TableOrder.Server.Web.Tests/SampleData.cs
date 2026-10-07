namespace TableOrder.Server.Web;

// サンプルのデータ (Assets/Data/SampleData.sql) の値
public static class SampleData
{
    public static readonly Guid DemoTenantId = Guid.Parse("00000000-0000-0000-0001-000000000001");

    public static readonly Guid DemoStoreId = Guid.Parse("00000000-0000-0000-0002-000000000001");

    public static readonly Guid TestStoreId = Guid.Parse("00000000-0000-0000-0002-000000000002");

    // デモのテナントのテーブル 1 の端末、ホール、キッチン、受付
    public const string DemoTableCode = "100001";

    public const string DemoHallCode = "100101";

    public const string DemoKitchenCode = "100201";

    public const string DemoReceptionCode = "100301";

    // 検証用のテナントのテーブル 1 の端末 (デモと同じ店舗コードの店舗)
    public const string TestTableCode = "200001";
}
