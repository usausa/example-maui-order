namespace TableOrder.Terminal.Table.State;

// 端末の設定 (IPreferences。キーはプロパティ名)。サーバができたら、テーブルと接続先は端末の登録で決まる
#pragma warning disable CA1724
public sealed class Settings
{
    private readonly IPreferences preferences;

    public Settings(IPreferences preferences)
    {
        this.preferences = preferences;
    }

    // この端末を置くテーブル
    public string TableNo
    {
        get => preferences.Get(nameof(TableNo), string.Empty);
        set => preferences.Set(nameof(TableNo), value);
    }

    // 注文サーバの URL。空ならモックの応答で動く
    public string ApiEndPoint
    {
        get => preferences.Get(nameof(ApiEndPoint), string.Empty);
        set => preferences.Set(nameof(ApiEndPoint), value);
    }

    public bool IsConfigured => !String.IsNullOrEmpty(TableNo);
}
#pragma warning restore CA1724
