#pragma warning disable IDE0130
// ReSharper disable once CheckNamespace
namespace TableOrder.TableApp;

using Android.App;

public static class AndroidHelper
{
    // 外部ストレージがない機種はアプリ内のフォルダを使う (ログと異常終了の記録の置き場所で、起動時に使う)
    public static string GetExternalFilesDir() =>
        Application.Context.GetExternalFilesDir(string.Empty)?.Path ?? Application.Context.FilesDir!.Path;
}
