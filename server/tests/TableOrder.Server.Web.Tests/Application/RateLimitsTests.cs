namespace TableOrder.Server.Web.Application;

using System.Net;

public sealed class RateLimitsTests
{
    // IPv6 は /64 を 1 つの接続元として数え、IPv4 (IPv6 に写したものも) はアドレスごとに数える
    [Theory]
    [InlineData("2001:db8:1:2:aaaa::1", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:2:bbbb:cccc:dddd:eeee", "2001:db8:1:2::/64")]
    [InlineData("2001:db8:1:3::1", "2001:db8:1:3::/64")]
    [InlineData("192.0.2.10", "192.0.2.10")]
    [InlineData("::ffff:192.0.2.10", "192.0.2.10")]
    public void PartitionGroupsIpv6By64(string address, string expected)
    {
        // Act
        var partition = RateLimits.PartitionOf(IPAddress.Parse(address));

        // Assert
        Assert.Equal(expected, partition);
    }

    // 接続元のわからない要求 (テストのサーバ) は 1 つの区分にする
    [Fact]
    public void UnknownAddressIsOnePartition()
    {
        // Act
        var partition = RateLimits.PartitionOf(null);

        // Assert
        Assert.Equal(string.Empty, partition);
    }
}
