namespace TableOrder.Server.Web.Endpoints;

using System.Net.Http.Headers;
using System.Text.Json;

using TableOrder.Contract.Menu;

public sealed class MenuEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public MenuEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 公開したメニューを返し、menuVersion を ETag にする
    [Fact]
    public async Task MenuReturnsPublishedContentWithETag()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);

        // Act
        using var response = await device.Client.GetAsync(new Uri("/api/v1/menu", UriKind.Relative), TestContext.Current.CancellationToken);
        var menu = await response.Content.ReadFromJsonAsync<MenuResponse>(TestDevice.JsonOptions, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("\"sample-1\"", response.Headers.ETag?.Tag);
        Assert.Equal("sample-1", menu!.MenuVersion);
        Assert.NotEmpty(menu.Items);
        Assert.Equal(3, menu.Stations.Count);
    }

    // 端末の持っているメニューと同じなら 304
    [Fact]
    public async Task MenuReturnsNotModifiedForSameVersion()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoTableCode);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/api/v1/menu", UriKind.Relative));
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"sample-1\""));

        // Act
        using var response = await device.Client.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
    }

    // 受付機はメニューを使わない (範囲の外は 403 DEVICE_SCOPE)
    [Fact]
    public async Task ReceptionCannotReadMenu()
    {
        // Arrange
        using var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(SampleData.DemoReceptionCode);

        // Act
        using var response = await device.Client.GetAsync(new Uri("/api/v1/menu", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken), cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal("DEVICE_SCOPE", document.RootElement.GetProperty("errorCode").GetString());
    }
}
