namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Events;
using TableOrder.Contract.Visits;

public sealed class VisitEndpointsTests : IClassFixture<ServerFactory>
{
    // 確認のルール (お酒の年齢) と、提案のルール (ドリンクバー)
    private static readonly Guid ConfirmationRuleId = Guid.Parse("000003e0-0000-0000-0000-000000000000");

    private static readonly Guid SuggestionRuleId = Guid.Parse("000003df-0000-0000-0000-000000000000");

    private readonly ServerFactory factory;

    public VisitEndpointsTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    //--------------------------------------------------------------------------------
    // Open
    //--------------------------------------------------------------------------------

    // ホール端末は案内したテーブルで来店を開き、同じ Id の送り直しは開いた来店を返す
    [Fact]
    public async Task HallOpensVisitAndResendReturnsSameVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var request = new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2, Children = 1 };

        // Act
        using var created = await hall.PostAsync("/api/v1/visits", request);
        using var resent = await hall.PostAsync("/api/v1/visits", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var visit = await TestDevice.ReadAsync<VisitResponse>(created);
        Assert.Equal(request.Id, visit.Id);
        Assert.Equal(store.TableIds[0], visit.TableId);
        Assert.Equal("1", visit.TableName);
        Assert.Equal(VisitStatus.Open, visit.Status);
        Assert.Equal(VisitOpenedBy.Hall, visit.OpenedBy);
        Assert.Empty(visit.ConfirmedRuleIds);
        Assert.Equal(0m, visit.OrderTotal);
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        Assert.Equal(visit.Version, (await TestDevice.ReadAsync<VisitResponse>(resent)).Version);
    }

    // 同じ Id で別のテーブルを送ると、送り直しとみなさない
    [Fact]
    public async Task ResendWithDifferentTableIsRejected()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var id = Guid.CreateVersion7();
        using var created = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = id, TableId = store.TableIds[0], Adults = 2 });

        // Act
        using var response = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = id, TableId = store.TableIds[1], Adults = 2 });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("DUPLICATE_ID_MISMATCH", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 来店のあるテーブルでは開かない
    [Fact]
    public async Task OccupiedTableIsRejected()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var created = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });

        // Act
        using var response = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("TABLE_OCCUPIED", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 人数は合わせて 1 人以上
    [Fact]
    public async Task NoGuestsIsRejected()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);

        // Act
        using var response = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0] });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 来店の開き方がスタッフの店では、テーブル端末と受付機は来店を開けない
    [Fact]
    public async Task TableAndReceptionCannotOpenVisitInHallStore()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        using var reception = await SignInAsync(store.ReceptionCode);

        // Act
        using var byTable = await table.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 1 });
        using var byReception = await reception.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, byTable.StatusCode);
        Assert.Equal("VISIT_OPENING_DISABLED", await TestDevice.ReadErrorCodeAsync(byTable));
        Assert.Equal(HttpStatusCode.Forbidden, byReception.StatusCode);
        Assert.Equal("VISIT_OPENING_DISABLED", await TestDevice.ReadErrorCodeAsync(byReception));
    }

    // 来店の開き方が席の店では、テーブル端末は自分のテーブルに来店を開き、ほかのテーブルには開けない
    [Fact]
    public async Task TableDeviceOpensVisitOnOwnTable()
    {
        // Arrange
        var store = await factory.CreateStoreAsync(VisitOpening.Table);
        using var table = await SignInAsync(store.TableCodes[1]);

        // Act
        using var other = await table.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });
        using var created = await table.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 2, Children = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(other));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var visit = await TestDevice.ReadAsync<VisitResponse>(created);
        Assert.Equal(store.TableIds[1], visit.TableId);
        Assert.Equal(VisitOpenedBy.Table, visit.OpenedBy);
        Assert.Equal((2, 1), (visit.Adults, visit.Children));
    }

    // 来店の開き方が受付機の店では、人数の入る空席のうち定員の小さいテーブル (同じなら並びの順) に開き、同じ Id の送り直しは開いた来店を返す
    [Fact]
    public async Task ReceptionOpensVisitOnSmallestVacantTable()
    {
        // Arrange
        var store = await factory.CreateStoreAsync(VisitOpening.Reception);
        using var reception = await SignInAsync(store.ReceptionCode);
        var request = new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 2 };

        // Act
        using var first = await reception.PostAsync("/api/v1/visits", request);
        using var resent = await reception.PostAsync("/api/v1/visits", request);
        using var large = await reception.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 4, Children = 1 });
        using var small = await reception.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var visit = await TestDevice.ReadAsync<VisitResponse>(first);
        Assert.Equal((store.TableIds[0], VisitOpenedBy.Reception), (visit.TableId, visit.OpenedBy));
        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        Assert.Equal(visit.Id, (await TestDevice.ReadAsync<VisitResponse>(resent)).Id);
        Assert.Equal(store.TableIds[2], (await TestDevice.ReadAsync<VisitResponse>(large)).TableId);
        Assert.Equal(store.TableIds[1], (await TestDevice.ReadAsync<VisitResponse>(small)).TableId);
    }

    // 受付機は、人数の入る空席がなければ開かず、テーブルを送っても開かない (ホール端末はどの形の店でも開ける)
    [Fact]
    public async Task ReceptionRejectsFullStoreAndChosenTable()
    {
        // Arrange
        var store = await factory.CreateStoreAsync(VisitOpening.Reception);
        using var reception = await SignInAsync(store.ReceptionCode);
        await factory.OpenVisitAsync(store, 2);

        // Act
        using var full = await reception.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), Adults = 5 });
        using var chosen = await reception.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, full.StatusCode);
        Assert.Equal("NO_VACANT_TABLE", await TestDevice.ReadErrorCodeAsync(full));
        Assert.Equal(HttpStatusCode.BadRequest, chosen.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(chosen));
    }

    // キッチン端末は来店を開けない
    [Fact]
    public async Task KitchenCannotOpenVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var kitchen = await SignInAsync(store.KitchenCode);

        // Act
        using var response = await kitchen.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 1 });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(response));
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    // テーブル端末の今の来店は、来店がなければ 204 で、開くとその来店になる
    [Fact]
    public async Task CurrentVisitOfTableDevice()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var table = await SignInAsync(store.TableCodes[0]);

        // Act / Assert: 来店がなければ待受
        using var none = await table.Client.GetAsync(new Uri("/api/v1/devices/me/visit", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);

        // Act / Assert: ホールが開くと、テーブル端末の来店になる
        var visit = await OpenAsync(hall, store.TableIds[0]);
        var current = await table.GetAsync<VisitResponse>("/api/v1/devices/me/visit");
        Assert.Equal(visit.Id, current.Id);
    }

    // テーブル端末はほかのテーブルの来店を読めず、ほかのテナントの来店は見つからない
    [Fact]
    public async Task VisitIsScopedToTableAndTenant()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var other = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var table = await SignInAsync(store.TableCodes[1]);
        using var otherHall = await SignInAsync(other.HallCode);
        var visit = await OpenAsync(hall, store.TableIds[0]);

        // Act
        var fromHall = await hall.GetAsync<VisitResponse>($"/api/v1/visits/{visit.Id}");
        using var fromTable = await table.Client.GetAsync(new Uri($"/api/v1/visits/{visit.Id}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var fromOtherTenant = await otherHall.Client.GetAsync(new Uri($"/api/v1/visits/{visit.Id}", UriKind.Relative), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(visit.Id, fromHall.Id);
        Assert.Equal(HttpStatusCode.Forbidden, fromTable.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(fromTable));
        Assert.Equal(HttpStatusCode.NotFound, fromOtherTenant.StatusCode);
        Assert.Equal("NOT_FOUND", await TestDevice.ReadErrorCodeAsync(fromOtherTenant));
    }

    //--------------------------------------------------------------------------------
    // Update
    //--------------------------------------------------------------------------------

    // 人数の変更は表示していた版で確かめ、変えると版が進む
    [Fact]
    public async Task UpdateGuestsChecksVersion()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var visit = await OpenAsync(hall, store.TableIds[0]);

        // Act
        using var updated = await hall.PatchAsync($"/api/v1/visits/{visit.Id}", new VisitUpdateRequest { Adults = 3, Children = 2, Version = visit.Version });
        using var stale = await hall.PatchAsync($"/api/v1/visits/{visit.Id}", new VisitUpdateRequest { Adults = 1, Version = visit.Version });

        // Assert
        var result = await TestDevice.ReadAsync<VisitResponse>(updated);
        Assert.Equal(3, result.Adults);
        Assert.Equal(2, result.Children);
        Assert.Equal(visit.Version + 1, result.Version);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("VERSION_MISMATCH", await TestDevice.ReadErrorCodeAsync(stale));
    }

    // 移動すると、元のテーブル端末は来店がなくなり、移動先のテーブル端末の来店になる。来店のあるテーブルには移さない
    [Fact]
    public async Task MoveChangesTableOfVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var from = await SignInAsync(store.TableCodes[0]);
        using var to = await SignInAsync(store.TableCodes[1]);
        var visit = await OpenAsync(hall, store.TableIds[0]);
        await OpenAsync(hall, store.TableIds[2]);

        // Act / Assert: 来店のあるテーブルには移さない
        using var occupied = await hall.PostAsync($"/api/v1/visits/{visit.Id}/move", new VisitMoveRequest { ToTableId = store.TableIds[2], Version = visit.Version });
        Assert.Equal(HttpStatusCode.Conflict, occupied.StatusCode);
        Assert.Equal("TABLE_OCCUPIED", await TestDevice.ReadErrorCodeAsync(occupied));

        // Act / Assert: 空いているテーブルに移す
        using var moved = await hall.PostAsync($"/api/v1/visits/{visit.Id}/move", new VisitMoveRequest { ToTableId = store.TableIds[1], Version = visit.Version });
        var result = await TestDevice.ReadAsync<VisitResponse>(moved);
        Assert.Equal(store.TableIds[1], result.TableId);
        Assert.Equal("2", result.TableName);
        using var fromVisit = await from.Client.GetAsync(new Uri("/api/v1/devices/me/visit", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, fromVisit.StatusCode);
        Assert.Equal(visit.Id, (await to.GetAsync<VisitResponse>("/api/v1/devices/me/visit")).Id);
    }

    // 確認のルールに答えた記録は来店で 1 回だけ持ち、確認のルールでないものは受けない
    [Fact]
    public async Task ConfirmRecordsConfirmationRule()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);

        // Act
        using var first = await table.PostAsync($"/api/v1/visits/{visit.Id}/confirmations", new VisitConfirmationRequest { RuleId = ConfirmationRuleId });
        using var second = await table.PostAsync($"/api/v1/visits/{visit.Id}/confirmations", new VisitConfirmationRequest { RuleId = ConfirmationRuleId });
        using var suggestion = await table.PostAsync($"/api/v1/visits/{visit.Id}/confirmations", new VisitConfirmationRequest { RuleId = SuggestionRuleId });

        // Assert
        Assert.Equal([ConfirmationRuleId], (await TestDevice.ReadAsync<VisitResponse>(first)).ConfirmedRuleIds);
        Assert.Equal([ConfirmationRuleId], (await TestDevice.ReadAsync<VisitResponse>(second)).ConfirmedRuleIds);
        Assert.Equal(HttpStatusCode.BadRequest, suggestion.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(suggestion));
    }

    // ホール端末も、代わりの注文でスタッフがお客様に確かめた確認のルールを記録できる (キッチン端末は記録できない)
    [Fact]
    public async Task HallRecordsConfirmationRule()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var kitchen = await SignInAsync(store.KitchenCode);
        var visit = await factory.OpenVisitAsync(store, 0);

        // Act
        using var byHall = await hall.PostAsync($"/api/v1/visits/{visit.Id}/confirmations", new VisitConfirmationRequest { RuleId = ConfirmationRuleId });
        using var byKitchen = await kitchen.PostAsync($"/api/v1/visits/{visit.Id}/confirmations", new VisitConfirmationRequest { RuleId = ConfirmationRuleId });

        // Assert
        Assert.Equal([ConfirmationRuleId], (await TestDevice.ReadAsync<VisitResponse>(byHall)).ConfirmedRuleIds);
        Assert.Equal(HttpStatusCode.Forbidden, byKitchen.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(byKitchen));
    }

    // ホール端末で新しく記録した確認は、来店の変更としてテーブル端末に知らせる (同じ来店で聞き直さないように)。答え直しは知らせない
    [Fact]
    public async Task ConfirmNotifiesTable()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);
        var before = await table.GetAsync<EventListResponse>("/api/v1/events?after=0");

        // Act
        using var first = await hall.PostAsync($"/api/v1/visits/{visit.Id}/confirmations", new VisitConfirmationRequest { RuleId = ConfirmationRuleId });
        using var second = await hall.PostAsync($"/api/v1/visits/{visit.Id}/confirmations", new VisitConfirmationRequest { RuleId = ConfirmationRuleId });
        var events = await table.GetAsync<EventListResponse>($"/api/v1/events?after={before.LastSeq}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal([EventTypes.VisitUpdated], events.Items.Select(static x => x.Type));
        Assert.Equal([ConfirmationRuleId], TestHubConnection.Read<VisitResponse>(events.Items[0]).ConfirmedRuleIds);
    }

    //--------------------------------------------------------------------------------
    // Close
    //--------------------------------------------------------------------------------

    // レジで払った来店を終えると、テーブルは空き、終えた来店は変えられない
    [Fact]
    public async Task CloseEndsVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var visit = await OpenAsync(hall, store.TableIds[0]);

        // Act
        using var closed = await hall.PostAsync($"/api/v1/visits/{visit.Id}/close", new VisitCloseRequest { ClosedBy = VisitClosedBy.Register, Version = visit.Version, StaffId = "S01" });
        using var update = await hall.PatchAsync($"/api/v1/visits/{visit.Id}", new VisitUpdateRequest { Adults = 1, Version = visit.Version + 1 });
        using var reopened = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[0], Adults = 2 });

        // Assert
        var result = await TestDevice.ReadAsync<VisitResponse>(closed);
        Assert.Equal(VisitStatus.Closed, result.Status);
        Assert.Equal(VisitClosedBy.Register, result.ClosedBy);
        Assert.NotNull(result.ClosedAt);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, update.StatusCode);
        Assert.Equal("VISIT_NOT_OPEN", await TestDevice.ReadErrorCodeAsync(update));
        Assert.Equal(HttpStatusCode.Created, reopened.StatusCode);
    }

    // テーブルで払い終えたことは、この入口では送れない (サーバが閉じる)
    [Fact]
    public async Task CloseRejectsTablePayment()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var visit = await OpenAsync(hall, store.TableIds[0]);

        // Act
        using var response = await hall.PostAsync($"/api/v1/visits/{visit.Id}/close", new VisitCloseRequest { ClosedBy = VisitClosedBy.TablePayment, Version = visit.Version });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", await TestDevice.ReadErrorCodeAsync(response));
    }

    // 注文のない来店は取りやめられる
    [Fact]
    public async Task CancelEndsVisitWithoutOrders()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var hall = await SignInAsync(store.HallCode);
        var visit = await OpenAsync(hall, store.TableIds[0]);

        // Act
        using var response = await hall.PostAsync($"/api/v1/visits/{visit.Id}/cancel", new VisitCancelRequest { Version = visit.Version });

        // Assert
        var result = await TestDevice.ReadAsync<VisitResponse>(response);
        Assert.Equal(VisitStatus.Cancelled, result.Status);
        Assert.Equal(VisitClosedBy.Hall, result.ClosedBy);
    }

    // テーブル端末は来店を変えられない
    [Fact]
    public async Task TableDeviceCannotChangeVisit()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var table = await SignInAsync(store.TableCodes[0]);
        var visit = await factory.OpenVisitAsync(store, 0);

        // Act
        using var response = await table.PostAsync($"/api/v1/visits/{visit.Id}/cancel", new VisitCancelRequest { Version = visit.Version });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("DEVICE_SCOPE", await TestDevice.ReadErrorCodeAsync(response));
    }

    //--------------------------------------------------------------------------------
    // Helper
    //--------------------------------------------------------------------------------

    private async Task<TestDevice> SignInAsync(string code)
    {
        var device = new TestDevice(factory.CreateClient());
        await device.SignInAsync(code);
        return device;
    }

    private static async Task<VisitResponse> OpenAsync(TestDevice device, Guid tableId)
    {
        using var response = await device.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = tableId, Adults = 2 });
        return await TestDevice.ReadAsync<VisitResponse>(response);
    }
}
