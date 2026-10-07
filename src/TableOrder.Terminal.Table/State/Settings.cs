namespace TableOrder.Terminal.Table.State;

using TableOrder.Terminal.Table.Components;

// 端末の設定 (IPreferences。キーはプロパティ名)。サーバができたら、テーブルと接続先は端末の登録で決まる
// 接続先と PIN は、EMM が管理対象の構成で配っていればそれを使う (端末の値は残し、配られなくなったら戻る)
#pragma warning disable CA1724
public sealed class Settings
{
    private const string DefaultStaffPin = "1234";

    private readonly IPreferences preferences;

    private readonly ManagedConfiguration managed;

    public Settings(
        IPreferences preferences,
        ManagedConfiguration managed)
    {
        this.preferences = preferences;
        this.managed = managed;
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
        get => managed.ApiEndPoint ?? preferences.Get(nameof(ApiEndPoint), string.Empty);
        set => preferences.Set(nameof(ApiEndPoint), value);
    }

    // 接続先を EMM が配っている (端末では変えられない)
    public bool IsApiEndPointManaged => managed.ApiEndPoint is not null;

    // スタッフメニューに入る PIN。EMM が配っていなければ端末ごとに持ち、初めは 1234 にする
    public string StaffPin
    {
        get => managed.StaffPin ?? preferences.Get(nameof(StaffPin), DefaultStaffPin);
        set => preferences.Set(nameof(StaffPin), value);
    }

    // PIN を EMM が配っている (端末では変えられない)
    public bool IsStaffPinManaged => managed.StaffPin is not null;

    public bool IsConfigured => !String.IsNullOrEmpty(TableNo);
}
#pragma warning restore CA1724
