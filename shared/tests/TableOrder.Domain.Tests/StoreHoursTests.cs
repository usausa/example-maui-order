namespace TableOrder.Domain;

public sealed class StoreHoursTests
{
    //--------------------------------------------------------------------------------
    // Format
    //--------------------------------------------------------------------------------

    // 時刻は HH:mm (時は 2 桁) で読み書きする
    [Fact]
    public void ParseAndFormat()
    {
        // Act
        var time = StoreHours.Parse("07:05");
        var text = StoreHours.Format(time);

        // Assert
        Assert.Equal(new TimeOnly(7, 5), time);
        Assert.Equal("07:05", text);
    }

    //--------------------------------------------------------------------------------
    // UntilLastOrder
    //--------------------------------------------------------------------------------

    // ラストオーダーまでの残り (11:00 開店、21:30 ラストオーダー)。ちょうどは 0 (過ぎていない)、開店前は前の営業日の続きとして過ぎている
    [Theory]
    [InlineData("11:00", 630)]
    [InlineData("20:00", 90)]
    [InlineData("21:30", 0)]
    [InlineData("21:45", -15)]
    [InlineData("09:00", -690)]
    public void UntilLastOrder(string now, int expectedMinutes)
    {
        var remaining = StoreHours.UntilLastOrder(StoreHours.Parse(now), StoreHours.Parse("11:00"), StoreHours.Parse("21:30"));

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), remaining);
    }

    // 日をまたぐ営業 (17:00 開店、1:30 ラストオーダー) は、日付が変わってもその営業日として数える
    [Theory]
    [InlineData("23:00", 150)]
    [InlineData("00:30", 60)]
    [InlineData("01:30", 0)]
    [InlineData("02:00", -30)]
    [InlineData("16:00", -870)]
    public void UntilLastOrderAcrossMidnight(string now, int expectedMinutes)
    {
        var remaining = StoreHours.UntilLastOrder(StoreHours.Parse(now), StoreHours.Parse("17:00"), StoreHours.Parse("01:30"));

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), remaining);
    }

    //--------------------------------------------------------------------------------
    // LocalTime
    //--------------------------------------------------------------------------------

    // 店舗のタイムゾーンの時刻にする (UTC の 0:00 は東京の 9:00)
    [Fact]
    public void LocalTimeInStoreTimeZone()
    {
        var time = StoreHours.LocalTime(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), "Asia/Tokyo");

        Assert.Equal(new TimeOnly(9, 0), time);
    }

    // タイムゾーンが見つからないときは端末の時刻にする
    [Fact]
    public void LocalTimeFallsBackToDevice()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // Act
        var time = StoreHours.LocalTime(now, "Unknown/Zone");

        // Assert
        Assert.Equal(TimeOnly.FromDateTime(now.ToLocalTime().DateTime), time);
    }
}
