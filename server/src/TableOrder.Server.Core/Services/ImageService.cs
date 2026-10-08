namespace TableOrder.Server.Core.Services;

// 読んだ画像 (中身と種類)
public sealed record ImageResult(ReadOnlyMemory<byte> Content, string ContentType);

// 画像 (料理の写真、チェーンのロゴ)。端末のテナントの置き場から名前で読む
public sealed class ImageService
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.Ordinal)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp"
    };

    private readonly ServiceContextProvider contextProvider;

    private readonly IImageStore imageStore;

    public ImageService(
        ServiceContextProvider contextProvider,
        IImageStore imageStore)
    {
        this.contextProvider = contextProvider;
        this.imageStore = imageStore;
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    public async ValueTask<ServiceResult<ImageResult>> GetAsync(string name, CancellationToken cancellationToken)
    {
        if (!ImageNames.IsValid(name))
        {
            return new(ServiceError.Validation("name", "画像の名前は英小文字・数字・ハイフン・下線・点で、拡張子を png / jpg / jpeg / webp にしてください"));
        }

        var content = await imageStore.ReadAsync(contextProvider.Current.RequireTenantId(), name, cancellationToken);
        return content is null ? new(ServiceError.NotFound) : new(new ImageResult(content, ContentTypes[Path.GetExtension(name)]));
    }

    // テナントに置いた画像の名前 (管理画面でロゴを選ぶ)
    public ValueTask<IReadOnlyList<string>> GetNamesAsync(CancellationToken cancellationToken) =>
        imageStore.ListAsync(contextProvider.Current.RequireTenantId(), cancellationToken);

    //--------------------------------------------------------------------------------
    // Sample
    //--------------------------------------------------------------------------------

    // サンプルの画像 (フォルダの中の画像) を、まだ置いていないテナントの置き場に写す (開発の環境とテスト)。写した数を返す
    public async ValueTask<int> CopySampleAsync(Guid tenantId, string directory, CancellationToken cancellationToken)
    {
        var copied = 0;
        foreach (var path in Directory.EnumerateFiles(directory))
        {
            var name = Path.GetFileName(path);
            if (!ImageNames.IsValid(name) || await imageStore.ExistsAsync(tenantId, name, cancellationToken))
            {
                continue;
            }

            await imageStore.WriteAsync(tenantId, name, await File.ReadAllBytesAsync(path, cancellationToken), cancellationToken);
            copied++;
        }

        return copied;
    }
}
