namespace TableOrder.Server.Web.Application.Context;

// 管理画面で選んだテナントと店舗 (回線単位)。選んでいれば、管理画面の業務の処理をその店舗の文脈で呼ぶ
// 利用者の扱える範囲の外は選ばない (画面で選んだ値をそのまま文脈にしない)
public sealed class StoreSelection
{
    private readonly AdminScope scope;

    public StoreSelection(AdminScope scope)
    {
        this.scope = scope;
    }

    public Guid? TenantId { get; private set; }

    public Guid? StoreId { get; private set; }

    // 選び直した (店舗の画面は読み直す)
    public event EventHandler? Changed;

    // 選べるテナントと店舗が替わった (足した、使わなくした。店舗の選択は一覧を読み直す)
    public event EventHandler? ChoicesChanged;

    public void NotifyChoicesChanged() => ChoicesChanged?.Invoke(this, EventArgs.Empty);

    // テナントを替えたら、店舗を選び直すまで店舗の文脈にしない
    public void Select(Guid? tenantId, Guid? storeId)
    {
        TenantId = tenantId is { } tenant && scope.CanUseTenant(tenant) ? tenant : null;
        StoreId = (TenantId is { } selected) && (storeId is { } store) && scope.CanUseStore(selected, store) ? store : null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
