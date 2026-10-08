namespace TableOrder.TableApp.Components;

// 料理の写真を端末に保存して使い回す。名前は内容が変わると変わるので、保存した画像は取り直さない
// 保存は登録ごとのフォルダに分け、登録し直したら前のフォルダを消す (ほかのチェーンの画像を残さない)
public sealed class ImageCache : IDisposable
{
    // 同時に受け取る数
    private const int Concurrency = 4;

    private readonly ILogger<ImageCache> log;

    private readonly IFileSystem fileSystem;

    private readonly Settings settings;

    private readonly IDeviceApi deviceApi;

    // 受け取りは 1 回ずつにする (起動と待受の取り直しを重ねない)
    private readonly SemaphoreSlim syncing = new(1, 1);

    // 前の受け取りで受け取れなかった数 (待受に戻ったときに取り直すか)
    private volatile int missing;

    public ImageCache(
        ILogger<ImageCache> log,
        IFileSystem fileSystem,
        Settings settings,
        IDeviceApi deviceApi)
    {
        this.log = log;
        this.fileSystem = fileSystem;
        this.settings = settings;
        this.deviceApi = deviceApi;
    }

    public void Dispose() => syncing.Dispose();

    private string Root => Path.Combine(fileSystem.AppDataDirectory, "images");

    private string? Folder => settings.DeviceId is { } deviceId ? Path.Combine(Root, deviceId.ToString("N")) : null;

    //--------------------------------------------------------------------------------
    // Read
    //--------------------------------------------------------------------------------

    // 保存した画像のファイル。保存していなければ null (画面は代わりの絵を出す)
    public string? PathOf(string? name)
    {
        if (!ImageNames.IsValid(name) || (Folder is not { } folder))
        {
            return null;
        }

        var path = Path.Combine(folder, name);
        return File.Exists(path) ? path : null;
    }

    //--------------------------------------------------------------------------------
    // Sync
    //--------------------------------------------------------------------------------

    // 足りない画像をまとめて受け取り、使わなくなった画像とほかの登録のフォルダを消す。受け取れなかった数を返す
    // 時間切れで止めても、受け取れたものは残す (受け取れなかったものは代わりの絵で出し、あとで取り直す)
    public async Task<int> SyncAsync(IReadOnlyCollection<string> names, IProgress<double>? progress = null, CancellationToken cancel = default)
    {
        if (Folder is not { } folder)
        {
            return names.Count;
        }

        await syncing.WaitAsync(CancellationToken.None);
        try
        {
            RemoveUnused(folder, names);

            var targets = names.Where(x => ImageNames.IsValid(x) && !File.Exists(Path.Combine(folder, x))).ToList();
            var done = 0;
            var saved = 0;
            try
            {
                await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = Concurrency, CancellationToken = cancel }, async (name, token) =>
                {
                    if (await DownloadAsync(folder, name, token))
                    {
                        Interlocked.Increment(ref saved);
                    }

                    progress?.Report((double)Interlocked.Increment(ref done) / targets.Count);
                });
            }
            catch (OperationCanceledException)
            {
                // 受け取れなかった分は数に残す
            }

            missing = targets.Count - saved;
            log.InfoImagesSynced(saved, missing);
            return missing;
        }
        finally
        {
            syncing.Release();
        }
    }

    // 前に受け取れなかった画像があれば、待たずに取り直す (待受に戻ったとき。次に注文の画面を作るときに出す)
    public void RetryInBackground(IReadOnlyCollection<string> names)
    {
        if (missing == 0)
        {
            return;
        }

        SyncAsync(names).ContinueWith(
            t => log.WarnImageSyncStopped(t.Exception!),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    // 書きかけのファイルを出さないように、別の名前で書き終えてから置き換える
    private async Task<bool> DownloadAsync(string folder, string name, CancellationToken cancel)
    {
        var result = await deviceApi.GetImageAsync(name, cancel);
        if (result.Content is not { } content)
        {
            log.WarnImageFailed(name, result.Status, result.ErrorCode);
            return false;
        }

        var path = Path.Combine(folder, name);
        var temporary = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, content, cancel);
            File.Move(temporary, path, true);
            return true;
        }
        catch (IOException ex)
        {
            log.WarnImageSaveFailed(name, ex);
            return false;
        }
    }

    // 今の画像の一覧にないファイル (使わなくなった画像、書きかけ) と、ほかの登録のフォルダを消す
    private void RemoveUnused(string folder, IEnumerable<string> names)
    {
        try
        {
            Directory.CreateDirectory(folder);

            var keep = names.ToHashSet(StringComparer.Ordinal);
            foreach (var file in Directory.EnumerateFiles(folder).Where(x => !keep.Contains(Path.GetFileName(x))))
            {
                File.Delete(file);
            }

            foreach (var other in Directory.EnumerateDirectories(Root).Where(x => !String.Equals(x, folder, StringComparison.Ordinal)))
            {
                Directory.Delete(other, true);
            }
        }
        catch (IOException ex)
        {
            log.WarnImageCleanupFailed(ex);
        }
    }
}
