namespace TableOrder.Server.Web.Application;

public sealed class BadRequestTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public BadRequestTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 読めない値 (数でない after) と知らない項目のある本文も、入力の誤り (VALIDATION_ERROR) にする (端末が扱いを決められるように)
    [Fact]
    public async Task UnreadableRequestIsValidationError()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);

        // Act
        using var badQuery = await hall.Client.GetAsync(new Uri("/api/v1/events?after=x", UriKind.Relative), TestContext.Current.CancellationToken);
        using var unknownField = await hall.PostAsync("/api/v1/visits", new { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2, Unknown = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, badQuery.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(badQuery));
        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(unknownField));
    }
}
