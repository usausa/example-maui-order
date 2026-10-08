namespace TableOrder.Domain;

public sealed class ImageNamesTests
{
    // 英小文字・数字・ハイフン・下線・点で、拡張子が png / jpg / jpeg / webp の名前だけを受ける
    [Theory]
    [InlineData("hamburg.93a3ef9c.png", true)]
    [InlineData("hamburg-egg_2.jpg", true)]
    [InlineData("0logo.jpeg", true)]
    [InlineData("photo.webp", true)]
    [InlineData("Hamburg.png", false)]
    [InlineData("hamburg.PNG", false)]
    [InlineData("hamburg.gif", false)]
    [InlineData("hamburg", false)]
    [InlineData(".hidden.png", false)]
    [InlineData("-dash.png", false)]
    [InlineData("../tenant/hamburg.png", false)]
    [InlineData("dir\\hamburg.png", false)]
    [InlineData("ham burg.png", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidatesName(string? name, bool expected)
    {
        // Act
        var valid = ImageNames.IsValid(name);

        // Assert
        Assert.Equal(expected, valid);
    }

    // 名前は決まった長さまで
    [Fact]
    public void RejectsTooLongName()
    {
        // Arrange
        var longest = new string('a', Length.ImageName - 4) + ".png";

        // Act / Assert
        Assert.True(ImageNames.IsValid(longest));
        Assert.False(ImageNames.IsValid("a" + longest));
    }
}
