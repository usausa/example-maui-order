namespace TableOrder.Server.Web.Settings;

public sealed class ImageSetting
{
    // 画像をファイルに置くときのフォルダ (実行のフォルダからの相対か、絶対の経路)
    [Required]
    public string Directory { get; set; } = default!;
}
