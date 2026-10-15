namespace TableOrder.Server.Web.Application;

using TableOrder.Server.Web.Endpoints;

public sealed class BadRequestTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public BadRequestTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // 読めない値 (数でない after) と知らない項目のある本文も、入力の誤り (VALIDATION_ERROR) にする (端末が扱いを決められるように)
    // 文言はフレームワークの英語ではなく、Service の入力の誤りと同じにする
    [Fact]
    public async Task UnreadableRequestIsValidationError()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);

        // Act
        using var badQuery = await hall.Client.GetAsync(new Uri($"{ApiRoutes.Events}?after=x", UriKind.Relative), TestContext.Current.CancellationToken);
        using var unknownField = await hall.PostAsync(ApiRoutes.Visits, new { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2, Unknown = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, badQuery.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(badQuery));
        Assert.Equal(ApiProblems.ValidationTitle, await TestDevice.ReadTitleAsync(badQuery));
        Assert.Equal(HttpStatusCode.BadRequest, unknownField.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(unknownField));
        Assert.Equal(ApiProblems.ValidationTitle, await TestDevice.ReadTitleAsync(unknownField));
    }
}
