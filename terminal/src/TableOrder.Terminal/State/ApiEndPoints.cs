namespace TableOrder.Terminal.State;

// 注文サーバの接続先の形 (端末の設定と EMM の構成で同じ確かめを使う)
public static class ApiEndPoints
{
    // Release は平文の通信を止めている (マニフェストの usesCleartextTraffic) ので、http は開発の Debug だけ受ける
    public static bool IsValid(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

#if DEBUG
        return (uri.Scheme == Uri.UriSchemeHttps) || (uri.Scheme == Uri.UriSchemeHttp);
#else
        return uri.Scheme == Uri.UriSchemeHttps;
#endif
    }
}
