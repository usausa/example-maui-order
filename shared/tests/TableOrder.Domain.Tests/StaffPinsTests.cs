namespace TableOrder.Domain;

public sealed class StaffPinsTests
{
    // 作ったハッシュは同じ PIN で合い、違う PIN では合わない。塩は毎回変わる
    [Fact]
    public void CreatedHashVerifiesSamePinOnly()
    {
        // Arrange
        var (salt, hash) = StaffPins.Create("1234", 1000);
        var (otherSalt, _) = StaffPins.Create("1234", 1000);

        // Act / Assert
        Assert.True(StaffPins.Verify("1234", 1000, salt, hash));
        Assert.False(StaffPins.Verify("1235", 1000, salt, hash));
        Assert.False(StaffPins.Verify("1234", 1001, salt, hash));
        Assert.NotEqual(salt, otherSalt);
    }

    // 形の崩れた塩とハッシュ、範囲の外の回数は合わないものにする
    [Theory]
    [InlineData(0, "AAAA", "AAAA")]
    [InlineData(2_000_000, "AAAA", "AAAA")]
    [InlineData(1000, "not base64!", "AAAA")]
    [InlineData(1000, "AAAA", "AAAA")]
    public void BrokenHashDoesNotVerify(int iterations, string salt, string hash)
    {
        // Act
        var verified = StaffPins.Verify("1234", iterations, salt, hash);

        // Assert
        Assert.False(verified);
    }

    // PIN は決まった桁数の数字
    [Theory]
    [InlineData("1234", true)]
    [InlineData("0000", true)]
    [InlineData("123", false)]
    [InlineData("12345", false)]
    [InlineData("12a4", false)]
    [InlineData(null, false)]
    public void ValidatesPin(string? pin, bool expected)
    {
        // Act
        var valid = StaffPins.IsValid(pin);

        // Assert
        Assert.Equal(expected, valid);
    }
}
