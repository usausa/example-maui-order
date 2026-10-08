namespace TableOrder.Server.Web.Endpoints;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Menu;
using TableOrder.Server.Core.Services;

// 画像 (料理の写真)。サンプルの写真は起動でサンプルのテナントの置き場に写してある
public sealed class ImageEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public ImageEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // メニューの写真の名前で、置いた画像をそのまま返し、端末に長く持たせる
    [Fact]
    public async Task MenuImageIsReturnedAsImmutable()
    {
        // Arrange
        using var table = new TestDevice(factory.CreateClient());
        await table.SignInAsync(SampleData.DemoTableCode);
        var name = (await table.GetAsync<MenuResponse>("/api/v1/menu")).Items.First(static x => x.ImageName is not null).ImageName!;

        // Act
        using var response = await table.Client.GetAsync(new Uri($"/api/v1/images/{name}", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.Private);
        Assert.Contains(response.Headers.CacheControl.Extensions, static x => x.Name == "immutable");
        var expected = await File.ReadAllBytesAsync(Path.Combine("Assets", "Images", name), TestContext.Current.CancellationToken);
        Assert.Equal(expected, await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }

    // 置いていない名前は 404、使えない文字の名前は 400
    [Fact]
    public async Task UnknownOrInvalidNameIsRejected()
    {
        // Arrange
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(SampleData.DemoHallCode);
        var cancel = TestContext.Current.CancellationToken;

        // Act
        using var unknown = await hall.Client.GetAsync(new Uri("/api/v1/images/unknown.12345678.png", UriKind.Relative), cancel);
        using var invalid = await hall.Client.GetAsync(new Uri("/api/v1/images/Hamburg.gif", UriKind.Relative), cancel);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal("NOT_FOUND", await TestDevice.ReadErrorCodeAsync(unknown));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(invalid));
    }

    // 画像はテナントごとに置き、ほかのテナントの画像は見つからない
    [Fact]
    public async Task ImageIsScopedToTenant()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await factory.Services.GetRequiredService<IImageStore>().WriteAsync(store.TenantId, "own.0badf00d.png", [1, 2, 3], TestContext.Current.CancellationToken);
        using var own = new TestDevice(factory.CreateClient());
        await own.SignInAsync(store.TableCodes[0]);
        using var other = new TestDevice(factory.CreateClient());
        await other.SignInAsync(SampleData.DemoTableCode);

        // Act
        using var found = await own.Client.GetAsync(new Uri("/api/v1/images/own.0badf00d.png", UriKind.Relative), TestContext.Current.CancellationToken);
        using var hidden = await other.Client.GetAsync(new Uri("/api/v1/images/own.0badf00d.png", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([1, 2, 3], await found.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }
}
