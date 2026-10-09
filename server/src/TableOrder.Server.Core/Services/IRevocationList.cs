namespace TableOrder.Server.Core.Services;

// 無効にした端末と止めたテナントのアクセストークンを、期限の前でもすぐに拒む一覧。持つのは入口の側で、業務の処理は替えたあとに読み直させる
public interface IRevocationList
{
    ValueTask RefreshAsync(CancellationToken cancellationToken);
}
