namespace TableOrder.Server.Web.Application.Context;

// 管理画面で選んだテナントと店舗 (回線単位)。選んでいれば、管理画面の業務の処理をその店舗の文脈で呼ぶ
public sealed class StoreSelection
{
    public Guid? TenantId { get; private set; }

    public Guid? StoreId { get; private set; }

    // 選び直した (店舗の画面は読み直す)
    public event EventHandler? Changed;

    // テナントを替えたら、店舗を選び直すまで店舗の文脈にしない
    public void Select(Guid? tenantId, Guid? storeId)
    {
        TenantId = tenantId;
        StoreId = tenantId is null ? null : storeId;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
