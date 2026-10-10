namespace TableOrder.Server.Web.Application;

// サーバに同梱するファイルの置き場 (起動のときに実行のフォルダを今のフォルダにするので、そこからの経路)
public static class AssetPaths
{
    public const string Schema = "Assets/Data/Schema.sql";

    public const string SampleData = "Assets/Data/SampleData.sql";

    // サンプルのメニュー (サンプルのデータと、写す店舗のない新しい店舗に入れる)
    public const string SampleMenu = "Assets/Data/Menu.json";

    // サンプルのデータの検証用のテナントの和食のメニュー (時間帯で出す品を持つ)
    public const string SampleWashokuMenu = "Assets/Data/MenuWashoku.json";

    public const string SampleImages = "Assets/Images";
}
