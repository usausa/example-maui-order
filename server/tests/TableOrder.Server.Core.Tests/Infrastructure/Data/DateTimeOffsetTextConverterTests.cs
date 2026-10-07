namespace TableOrder.Server.Core.Infrastructure.Data;

public sealed class DateTimeOffsetTextConverterTests
{
    // 日時は UTC にして、桁を揃えて書く (時差つきの値も UTC の文字列にし、文字列のまま比べられるようにする)
    [Fact]
    public void WritesUtcWithFixedDigits()
    {
        // Arrange
        var value = new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.FromHours(9));

        // Act
        var text = DateTimeOffsetTextConverter.ToDb(value);

        // Assert
        Assert.Equal("2026-03-01 00:00:00.0000000", text);
        Assert.Equal(value, DateTimeOffsetTextConverter.FromDb(text));
        Assert.Equal(TimeSpan.Zero, DateTimeOffsetTextConverter.FromDb(text).Offset);
    }
}
