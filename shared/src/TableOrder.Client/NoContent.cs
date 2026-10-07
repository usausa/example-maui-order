namespace TableOrder.Client;

// 内容のない応答 (REST の 204)
public sealed class NoContent
{
    public static NoContent Value { get; } = new();

    private NoContent()
    {
    }
}
