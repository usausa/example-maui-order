namespace TableOrder.Server.Web.Services;

using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Stores;
using TableOrder.Server.Core.Accessors;
using TableOrder.Server.Core.Services;

public sealed class TableSetupServiceTests : IClassFixture<ServerFactory>
{
    private readonly ServerFactory factory;

    public TableSetupServiceTests(ServerFactory factory)
    {
        this.factory = factory;
    }

    private TableSetupService Service => factory.Services.GetRequiredService<TableSetupService>();

    // テーブルを足すと並びの最後に入り、替えると設定の版が上がって、ホール端末のテーブルの一覧に出る
    [Fact]
    public async Task AddAndUpdateTables()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var version = await SettingsVersionAsync(store);
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);

        // Act
        using (factory.BeginStore(store))
        {
            Assert.Null(await Service.AddAsync(new TableInput("4", "窓側", 2), TestContext.Current.CancellationToken));
            var added = (await Service.GetListAsync(TestContext.Current.CancellationToken)).Single(x => x.Name == "4");
            Assert.Null(await Service.UpdateAsync(added.Id, new TableInput("A1", null, 3), added.Version, TestContext.Current.CancellationToken));
        }

        // Assert
        List<Core.Models.Entity.TableSetupEntity> tables;
        using (factory.BeginStore(store))
        {
            tables = await Service.GetListAsync(TestContext.Current.CancellationToken);
        }

        var table = Assert.Single(tables, x => x.Name == "A1");
        Assert.Equal(4, table.SortOrder);
        Assert.Null(table.Area);
        Assert.Equal(3, table.Capacity);
        Assert.Equal(version + 2, await SettingsVersionAsync(store));
        var list = await hall.GetAsync<TableListResponse>("/api/v1/tables");
        Assert.Contains(list.Items, x => x.Name == "A1");
    }

    // 同じ名前のテーブルは足せず、席の数は決まりの中にする
    [Fact]
    public async Task AddRejectsInvalidTables()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var scope = factory.BeginStore(store);

        // Act
        var duplicate = await Service.AddAsync(new TableInput("1", null, 4), TestContext.Current.CancellationToken);
        var capacity = await Service.AddAsync(new TableInput("9", null, 0), TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains("name", duplicate!.Errors!.Keys);
        Assert.Contains("capacity", capacity!.Errors!.Keys);
    }

    // 並べ替えは前後のテーブルと入れ替え、端より先には動かさない
    [Fact]
    public async Task MoveSwapsSortOrder()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        using var scope = factory.BeginStore(store);

        // Act
        var moved = await Service.MoveAsync(store.TableIds[2], -1, TestContext.Current.CancellationToken);
        var first = await Service.MoveAsync(store.TableIds[0], -1, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(moved);
        Assert.Contains("offset", first!.Errors!.Keys);
        Assert.Equal(["1", "3", "2"], (await Service.GetListAsync(TestContext.Current.CancellationToken)).Select(static x => x.Name));
    }

    // 来店中のテーブルと端末を置いたテーブルは使わなくできず、ほかは使わなくすると端末の一覧から外れる
    [Fact]
    public async Task DeactivateRequiresNoVisitAndDevice()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        await factory.OpenVisitAsync(store, 0);
        using var table = new TestDevice(factory.CreateClient());
        await table.SignInAsync(store.TableCodes[1]);
        using var hall = new TestDevice(factory.CreateClient());
        await hall.SignInAsync(store.HallCode);
        List<Core.Models.Entity.TableSetupEntity> tables;
        using (factory.BeginStore(store))
        {
            tables = await Service.GetListAsync(TestContext.Current.CancellationToken);
        }

        // Act
        ServiceError? visiting;
        ServiceError? placed;
        ServiceError? free;
        using (factory.BeginStore(store))
        {
            visiting = await Service.SetActiveAsync(store.TableIds[0], false, tables[0].Version, TestContext.Current.CancellationToken);
            placed = await Service.SetActiveAsync(store.TableIds[1], false, tables[1].Version, TestContext.Current.CancellationToken);
            free = await Service.SetActiveAsync(store.TableIds[2], false, tables[2].Version, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Contains("isActive", visiting!.Errors!.Keys);
        Assert.Contains("isActive", placed!.Errors!.Keys);
        Assert.Null(free);
        var list = await hall.GetAsync<TableListResponse>("/api/v1/tables");
        Assert.DoesNotContain(list.Items, x => x.Id == store.TableIds[2]);
    }

    // 替えられなかったときは、ほかの店舗のテーブルを見つからないとし、店舗のテーブルの古い版を版の違いにする
    [Fact]
    public async Task UpdateFailureDistinguishesNotFoundFromVersion()
    {
        // Arrange
        var store = await factory.CreateStoreAsync();
        var other = await factory.CreateStoreAsync();
        Core.Models.Entity.TableSetupEntity table;
        using (factory.BeginStore(store))
        {
            table = (await Service.GetListAsync(TestContext.Current.CancellationToken))[0];
        }

        // Act
        ServiceError? updatedFromOther;
        ServiceError? deactivatedFromOther;
        ServiceError? stale;
        using (factory.BeginStore(other))
        {
            updatedFromOther = await Service.UpdateAsync(table.Id, new TableInput("X1", null, 2), table.Version, TestContext.Current.CancellationToken);
            deactivatedFromOther = await Service.SetActiveAsync(table.Id, false, table.Version, TestContext.Current.CancellationToken);
        }

        using (factory.BeginStore(store))
        {
            stale = await Service.UpdateAsync(table.Id, new TableInput("X1", null, 2), table.Version - 1, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Equal(ErrorCodes.NotFound, updatedFromOther?.ErrorCode);
        Assert.Equal(ErrorCodes.NotFound, deactivatedFromOther?.ErrorCode);
        Assert.Equal(ErrorCodes.VersionMismatch, stale?.ErrorCode);
    }

    private async ValueTask<int> SettingsVersionAsync(TestStore store) =>
        (await factory.Services.GetRequiredService<StoreAccessor>().QueryAsync(store.TenantId, store.StoreId, TestContext.Current.CancellationToken))!.SettingsVersion;
}
