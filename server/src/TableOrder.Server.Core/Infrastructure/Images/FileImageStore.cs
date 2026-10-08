namespace TableOrder.Server.Core.Infrastructure.Images;

using TableOrder.Server.Core.Services;

// ファイルの置き場 (開発の環境とテスト)。{根}/{テナント}/{名前} に置く
public sealed class FileImageStore : IImageStore
{
    private readonly string root;

    public FileImageStore(string root)
    {
        this.root = root;
    }

    public async ValueTask<byte[]?> ReadAsync(Guid tenantId, string name, CancellationToken cancellationToken)
    {
        var path = PathOf(tenantId, name);
        return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken) : null;
    }

    public ValueTask<bool> ExistsAsync(Guid tenantId, string name, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested
            ? ValueTask.FromCanceled<bool>(cancellationToken)
            : ValueTask.FromResult(File.Exists(PathOf(tenantId, name)));

    // 書きかけのファイル (.tmp) は名前の決まりに合わないので出さない
    public ValueTask<IReadOnlyList<string>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(root, tenantId.ToString("D"));
        IReadOnlyList<string> names = Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory).Select(Path.GetFileName).OfType<string>().Where(ImageNames.IsValid).Order(StringComparer.Ordinal).ToList()
            : [];
        return cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled<IReadOnlyList<string>>(cancellationToken) : ValueTask.FromResult(names);
    }

    // 書きかけのファイルを読ませないように、別の名前で書き終えてから置き換える
    public async ValueTask WriteAsync(Guid tenantId, string name, byte[] content, CancellationToken cancellationToken)
    {
        var path = PathOf(tenantId, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + ".tmp";
        await File.WriteAllBytesAsync(temporary, content, cancellationToken);
        File.Move(temporary, path, true);
    }

    private string PathOf(Guid tenantId, string name) =>
        Path.Combine(root, tenantId.ToString("D"), name);
}
