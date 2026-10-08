namespace TableOrder.Server.Core.Services;

// 画像 (料理の写真、チェーンのロゴ) の置き場。テナントごとに分け、開発とテストはファイル、本番は Amazon S3 にする
// 名前は内容が変わると変わるので、置いた画像は替えない (名前は ImageNames で確かめてから渡す)。画像は小さいので中身をまとめて読み書きする
public interface IImageStore
{
    // なければ null
    ValueTask<byte[]?> ReadAsync(Guid tenantId, string name, CancellationToken cancellationToken);

    ValueTask<bool> ExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken);

    // テナントに置いた画像の名前 (管理画面でロゴを選ぶ)
    ValueTask<IReadOnlyList<string>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    ValueTask WriteAsync(Guid tenantId, string name, byte[] content, CancellationToken cancellationToken);
}
