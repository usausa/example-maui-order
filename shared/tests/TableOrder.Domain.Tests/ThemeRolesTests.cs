namespace TableOrder.Domain;

public sealed class ThemeRolesTests
{
    // 替えられる役割は Brand・Neutral・Status の色で、System の役割は替えない
    [Fact]
    public void SystemRolesAreNotReplaceable()
    {
        // Act / Assert
        Assert.True(ThemeRoles.IsRole("PrimaryColor"));
        Assert.True(ThemeRoles.IsRole("OnWarningColor"));
        Assert.False(ThemeRoles.IsRole("SystemAccentColor"));
        Assert.False(ThemeRoles.IsRole("primaryColor"));
        Assert.Equal(ThemeRoles.All.Count, ThemeRoles.All.Distinct().Count());
    }

    // 色は #RRGGBB か #AARRGGBB
    [Theory]
    [InlineData("#1E5FA8", true)]
    [InlineData("#991E5FA8", true)]
    [InlineData("#1e5fa8", true)]
    [InlineData("1E5FA8", false)]
    [InlineData("#1E5FA", false)]
    [InlineData("#GGGGGG", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidatesColor(string? value, bool expected)
    {
        // Act
        var valid = ThemeRoles.IsColor(value);

        // Assert
        Assert.Equal(expected, valid);
    }
}
