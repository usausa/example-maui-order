namespace TableOrder.Server.Web.Application;

using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

public sealed class BlazorOptionsTests
{
    // 管理画面の回線と Blazor のハブは、設定の既定 (枠組みの既定と同じ値) で動く
    [Fact]
    public async Task CircuitAndHubUseSettings()
    {
        // Arrange
        await using var factory = new ServerFactory();

        // Act
        var circuit = factory.Services.GetRequiredService<IOptions<CircuitOptions>>().Value;
        var hub = ResolveHubOptions(factory.Services);

        // Assert
        Assert.Equal(100, circuit.DisconnectedCircuitMaxRetained);
        Assert.Equal(TimeSpan.FromMinutes(3), circuit.DisconnectedCircuitRetentionPeriod);
        Assert.Equal(TimeSpan.FromMinutes(1), circuit.JSInteropDefaultCallTimeout);
        Assert.Equal(10, circuit.MaxBufferedUnacknowledgedRenderBatches);
        Assert.Equal(1000, circuit.PersistedCircuitInMemoryMaxRetained);
        Assert.Equal(TimeSpan.FromHours(2), circuit.PersistedCircuitInMemoryRetentionPeriod);
        Assert.Equal(TimeSpan.FromSeconds(30), hub.ClientTimeoutInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), hub.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), hub.HandshakeTimeout);
        Assert.Equal(32768, hub.MaximumReceiveMessageSize);
        Assert.Equal(1, hub.MaximumParallelInvocationsPerClient);
    }

    // 回線と Blazor のハブの値は設定で替えられる
    [Fact]
    public async Task SettingsChangeCircuitAndHub()
    {
        // Arrange
        await using var factory = new ServerFactory();
        factory.Settings["Blazor:Circuit:DisconnectedCircuitRetentionPeriod"] = "00:10:00";
        factory.Settings["Blazor:Hub:MaximumReceiveMessageSize"] = "65536";

        // Act
        var circuit = factory.Services.GetRequiredService<IOptions<CircuitOptions>>().Value;
        var hub = ResolveHubOptions(factory.Services);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(10), circuit.DisconnectedCircuitRetentionPeriod);
        Assert.Equal(65536, hub.MaximumReceiveMessageSize);
    }

    // Blazor のハブ (ComponentHub) の型は公開されていないので、名前で引いて設定を読む
    private static HubOptions ResolveHubOptions(IServiceProvider services)
    {
        var hubType = typeof(CircuitOptions).Assembly.GetType("Microsoft.AspNetCore.Components.Server.ComponentHub", throwOnError: true)!;
        var optionsType = typeof(IOptions<>).MakeGenericType(typeof(HubOptions<>).MakeGenericType(hubType));
        var options = services.GetRequiredService(optionsType);
        return (HubOptions)optionsType.GetProperty(nameof(IOptions<>.Value))!.GetValue(options)!;
    }
}
