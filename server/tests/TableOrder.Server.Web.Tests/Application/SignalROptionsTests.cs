namespace TableOrder.Server.Web.Application;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class SignalROptionsTests
{
    // 通知のハブはハブごとの値を持たず全体の値で動き、全体の値は設定の既定 (枠組みの既定と同じ値) になる
    [Fact]
    public async Task HubsUseSettings()
    {
        // Arrange
        await using var factory = new ServerFactory();

        // Act
        var hub = factory.Services.GetRequiredService<IOptions<HubOptions>>().Value;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(30), hub.ClientTimeoutInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), hub.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), hub.HandshakeTimeout);
        Assert.Equal(32768, hub.MaximumReceiveMessageSize);
        Assert.Equal(1, hub.MaximumParallelInvocationsPerClient);
    }

    // ハブの全体の値は設定で替えられる
    [Fact]
    public async Task SettingsChangeHubs()
    {
        // Arrange
        await using var factory = new ServerFactory();
        factory.Settings["SignalR:KeepAliveInterval"] = "00:00:20";
        factory.Settings["SignalR:ClientTimeoutInterval"] = "00:00:40";

        // Act
        var hub = factory.Services.GetRequiredService<IOptions<HubOptions>>().Value;

        // Assert
        Assert.Equal(TimeSpan.FromSeconds(20), hub.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(40), hub.ClientTimeoutInterval);
    }
}
