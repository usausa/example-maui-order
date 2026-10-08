namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Calls;

public sealed class CallEndpointsTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public CallEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    // テーブル端末の呼び出しはホールの一覧に出て、向かう・対応したで進む。同じ用件を続けて押しても増やさない
    [Fact]
    public async Task TableCallsAndHallResponds()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[1]);
        using var hall = await SignInAsync(store.HallCode);
        var visit = await factory.OpenVisitAsync(store, 1);

        // Act / Assert: 呼び出す (同じ用件は増やさない)
        using var created = await table.PostAsync($"/api/v1/visits/{visit.Id}/calls", new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Water" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var call = await TestDevice.ReadAsync<CallListResponseItem>(created);
        using var again = await table.PostAsync($"/api/v1/visits/{visit.Id}/calls", new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Water" });
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(call.Id, (await TestDevice.ReadAsync<CallListResponseItem>(again)).Id);
        Assert.Single((await table.GetAsync<CallListResponse>($"/api/v1/visits/{visit.Id}/calls")).Items);

        // Act / Assert: ホールの一覧に出て、向かう
        var open = await hall.GetAsync<CallListResponse>("/api/v1/calls");
        Assert.Equal("2", Assert.Single(open.Items).TableName);
        using var acknowledged = await hall.PostAsync($"/api/v1/calls/{call.Id}/acknowledge", new { });
        Assert.Equal(CallStatus.Acknowledged, (await TestDevice.ReadAsync<CallListResponseItem>(acknowledged)).Status);
        Assert.Equal(CallStatus.Acknowledged, Assert.Single((await table.GetAsync<CallListResponse>($"/api/v1/visits/{visit.Id}/calls")).Items).Status);

        // Act / Assert: 対応すると終わっていない一覧から外れ、同じ用件をまた呼べる
        using var done = await hall.PostAsync($"/api/v1/calls/{call.Id}/done", new { });
        Assert.Equal(CallStatus.Done, (await TestDevice.ReadAsync<CallListResponseItem>(done)).Status);
        Assert.Empty((await hall.GetAsync<CallListResponse>("/api/v1/calls")).Items);
        Assert.Equal([call.Id], (await hall.GetAsync<CallListResponse>("/api/v1/calls?status=Done")).Items.Select(static x => x.Id));
        using var next = await table.PostAsync($"/api/v1/visits/{visit.Id}/calls", new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Water" });
        Assert.Equal(HttpStatusCode.Created, next.StatusCode);
    }

    // 店舗にない用件は受けない
    [Fact]
    public async Task UnknownReasonIsRejected()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);

        // Act
        using var response = await table.PostAsync($"/api/v1/visits/{visit.Id}/calls", new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Unknown" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(response));
    }

    // ホール端末は呼び出しを作らず、テーブル端末は店舗の一覧を読めない
    [Fact]
    public async Task CallsAreScopedByDevice()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        using var hall = await SignInAsync(store.HallCode);
        var visit = await factory.OpenVisitAsync(store, 0);

        // Act
        using var fromHall = await hall.PostAsync($"/api/v1/visits/{visit.Id}/calls", new CallCreateRequest { Id = Guid.CreateVersion7(), ReasonCode = "Staff" });
        using var fromTable = await table.Client.GetAsync(new Uri("/api/v1/calls", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, fromHall.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, fromTable.StatusCode);
    }

    private async Task<TestDevice> SignInAsync(string code)
    {
        var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        return device;
    }
}
