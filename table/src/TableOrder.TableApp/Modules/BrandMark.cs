namespace TableOrder.TableApp.Modules;

using System.Globalization;

// ヘッダと待受のチェーンの名前と印。ロゴを保存していなければ、印に名前の頭の文字を出す
public sealed class BrandMark
{
    public string Name { get; }

    public string? LogoPath { get; }

    public bool HasLogo => LogoPath is not null;

    public string Initial { get; }

    public BrandMark(string name, string? logoPath)
    {
        Name = name;
        LogoPath = logoPath;
        Initial = name.Length > 0 ? StringInfo.GetNextTextElement(name) : string.Empty;
    }
}
