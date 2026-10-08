namespace TableOrder.Server.Web;

using System.Globalization;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Visits;
using TableOrder.Server.Core.Services;
using TableOrder.Server.Web.Application.Context;

// テストのクラスごとのサーバ。一時ファイルの DB に、サンプルのデータ (2 つのテナント) を入れて起動する
public sealed class ServerFactory : WebApplicationFactory<Program>
{
    private const string Now = "2026-01-01 00:00:00.0000000";

    // テストの店舗のスタッフの PIN (ハッシュは確かめが速いように回数を少なくする)
    public const string StaffPin = "1234";

    private static readonly string StaffPinHash = CreateStaffPinHash();

    // サンプルのデータのコード (1xxxxx、2xxxxx) と重ならないペアリングコード
    private int lastCode = 300_000;

    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"tableorder-test-{Guid.NewGuid():N}.db");

    // 画像の置き場 (クラスごとの一時のフォルダ。サンプルの写真を起動で写す)
    private readonly string imagePath = Path.Combine(Path.GetTempPath(), $"tableorder-test-images-{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Data Source={databasePath};Pooling=False;Foreign Keys=True",
            ["Database:SampleData"] = "true",
            ["Image:Directory"] = imagePath,
            ["RateLimit:PairingPerMinute"] = "10000",
            ["Log:HttpLog"] = "false",
            ["Simulation:Enabled"] = "false",
            ["Serilog:MinimumLevel:Default"] = "Warning"
        }));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        DeleteDatabase();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            DeleteDatabase();
        }
    }

    private void DeleteDatabase()
    {
        foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
        {
            File.Delete(path);
        }

        if (Directory.Exists(imagePath))
        {
            Directory.Delete(imagePath, true);
        }
    }

    //--------------------------------------------------------------------------------
    // Arrange (まだ API のない管理画面の操作を、DB に直接書く)
    //--------------------------------------------------------------------------------

    public async ValueTask RevokeDeviceAsync(Guid deviceId)
    {
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = "UPDATE Devices SET IsActive = 0 WHERE Id = @id";
        command.Parameters.AddWithValue("@id", deviceId.ToString("D"));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask SuspendTenantAsync(Guid tenantId)
    {
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = "UPDATE Tenants SET Status = 'Suspended' WHERE Id = @id";
        command.Parameters.AddWithValue("@id", tenantId.ToString("D"));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // ほかのテストに関わらないテナント (店舗と、ホール端末のペアリングコード) を作る
    public async ValueTask<(Guid TenantId, string PairingCode)> CreateTenantAsync()
    {
        var tenantId = Guid.CreateVersion7();
        var code = NextCode();

        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = """
            INSERT INTO Tenants (Id, Code, Name, BrandName, Status, CreatedAt, UpdatedAt, Version)
                VALUES (@tenantId, @tenantCode, 'test', '{"ja":"チェーン"}', 'Active', @now, @now, 1);
            INSERT INTO Stores (TenantId, Id, Code, Name, TimeZone, OpenTime, CloseTime, OrderingPaused, TaxRounding, MaxQuantityPerLine, MaxLinesPerOrder, Languages, PaymentMethods, ElectronicReceipt, Features, StaffPinHash, SettingsVersion, IsActive, CreatedAt, UpdatedAt, Version)
                VALUES (@tenantId, @storeId, '001', '{"ja":"店"}', 'Asia/Tokyo', '05:00', '04:00', 0, 'Floor', 9, 20, '["ja"]', '[]', 0, '{}', @staffPinHash, 1, 1, @now, @now, 1);
            INSERT INTO DeviceEnrollments (TenantId, Id, StoreId, Kind, Method, PairingCode, MaxUses, UsedCount, ExpiresAt, CreatedAt)
                VALUES (@tenantId, @enrollmentId, @storeId, 'Hall', 'PairingCode', @code, 10, 0, '9999-12-31 00:00:00.0000000', @now);
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId.ToString("D"));
        command.Parameters.AddWithValue("@tenantCode", tenantId.ToString("N"));
        command.Parameters.AddWithValue("@storeId", Guid.CreateVersion7().ToString("D"));
        command.Parameters.AddWithValue("@enrollmentId", Guid.CreateVersion7().ToString("D"));
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@staffPinHash", StaffPinHash);
        command.Parameters.AddWithValue("@now", Now);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return (tenantId, code);
    }

    // ほかのテストに関わらない店舗。テーブルを 3 つ置き、今のメニューはデモの店舗と同じにして、呼び出しの用件 (Staff、Water) と端末の種類ごとのペアリングコードを出す
    public async ValueTask<TestStore> CreateStoreAsync()
    {
        var tenantId = Guid.CreateVersion7();
        var storeId = Guid.CreateVersion7();
        var publicationId = Guid.CreateVersion7();
        var tableIds = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };
        var tableCodes = tableIds.Select(_ => NextCode()).ToArray();
        var hallCode = NextCode();
        var kitchenCode = NextCode();
        var receptionCode = NextCode();

        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = """
            BEGIN;
            INSERT INTO Tenants (Id, Code, Name, BrandName, Status, CreatedAt, UpdatedAt, Version)
                VALUES (@tenantId, @tenantCode, 'test', '{"ja":"チェーン"}', 'Active', @now, @now, 1);
            INSERT INTO Stores (TenantId, Id, Code, Name, TimeZone, OpenTime, CloseTime, OrderingPaused, TaxRounding, MaxQuantityPerLine, MaxLinesPerOrder, Languages, PaymentMethods, ElectronicReceipt, Features, StaffPinHash, SettingsVersion, MenuPublicationId, IsActive, CreatedAt, UpdatedAt, Version)
                VALUES (@tenantId, @storeId, '001', '{"ja":"店"}', 'Asia/Tokyo', '05:00', '04:00', 0, 'Floor', 9, 20, '["ja"]', '["QrCode","CreditCard"]', 1, '{}', @staffPinHash, 1, @publicationId, 1, @now, @now, 1);
            INSERT INTO CallReasons (TenantId, StoreId, Code, Name, SortOrder, IsActive)
                VALUES (@tenantId, @storeId, 'Staff', '{"ja":"店員を呼ぶ"}', 1, 1),
                       (@tenantId, @storeId, 'Water', '{"ja":"お水"}', 2, 1);
            INSERT INTO MenuPublications (TenantId, Id, StoreId, MenuVersion, Content, PublishedAt)
                SELECT @tenantId, @publicationId, @storeId, MenuVersion, Content, PublishedAt FROM MenuPublications WHERE Id = '00000000-0000-0000-0009-000000000001';
            INSERT INTO DiningTables (TenantId, Id, StoreId, Name, Capacity, SortOrder, IsActive, CreatedAt, UpdatedAt, Version)
                VALUES (@tenantId, @table1, @storeId, '1', 4, 1, 1, @now, @now, 1),
                       (@tenantId, @table2, @storeId, '2', 4, 2, 1, @now, @now, 1),
                       (@tenantId, @table3, @storeId, '3', 6, 3, 1, @now, @now, 1);
            INSERT INTO DeviceEnrollments (TenantId, Id, StoreId, Kind, Method, PairingCode, TableId, StationIds, MaxUses, UsedCount, ExpiresAt, CreatedAt)
                VALUES (@tenantId, @enrollment1, @storeId, 'Table', 'PairingCode', @tableCode1, @table1, NULL, 10, 0, '9999-12-31 00:00:00.0000000', @now),
                       (@tenantId, @enrollment2, @storeId, 'Table', 'PairingCode', @tableCode2, @table2, NULL, 10, 0, '9999-12-31 00:00:00.0000000', @now),
                       (@tenantId, @enrollment3, @storeId, 'Table', 'PairingCode', @tableCode3, @table3, NULL, 10, 0, '9999-12-31 00:00:00.0000000', @now),
                       (@tenantId, @enrollment4, @storeId, 'Hall', 'PairingCode', @hallCode, NULL, NULL, 10, 0, '9999-12-31 00:00:00.0000000', @now),
                       (@tenantId, @enrollment5, @storeId, 'Kitchen', 'PairingCode', @kitchenCode, NULL, '["0000005b-0000-0000-0000-000000000000","0000005c-0000-0000-0000-000000000000","0000005d-0000-0000-0000-000000000000"]', 10, 0, '9999-12-31 00:00:00.0000000', @now),
                       (@tenantId, @enrollment6, @storeId, 'Reception', 'PairingCode', @receptionCode, NULL, NULL, 10, 0, '9999-12-31 00:00:00.0000000', @now);
            COMMIT;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId.ToString("D"));
        command.Parameters.AddWithValue("@tenantCode", tenantId.ToString("N"));
        command.Parameters.AddWithValue("@storeId", storeId.ToString("D"));
        command.Parameters.AddWithValue("@publicationId", publicationId.ToString("D"));
        command.Parameters.AddWithValue("@staffPinHash", StaffPinHash);
        for (var i = 0; i < tableIds.Length; i++)
        {
            command.Parameters.AddWithValue($"@table{i + 1}", tableIds[i].ToString("D"));
            command.Parameters.AddWithValue($"@tableCode{i + 1}", tableCodes[i]);
        }

        for (var i = 1; i <= 6; i++)
        {
            command.Parameters.AddWithValue($"@enrollment{i}", Guid.CreateVersion7().ToString("D"));
        }

        command.Parameters.AddWithValue("@hallCode", hallCode);
        command.Parameters.AddWithValue("@kitchenCode", kitchenCode);
        command.Parameters.AddWithValue("@receptionCode", receptionCode);
        command.Parameters.AddWithValue("@now", Now);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return new TestStore(tenantId, storeId, tableIds, tableCodes, hallCode, kitchenCode, receptionCode);
    }

    //--------------------------------------------------------------------------------
    // Context
    //--------------------------------------------------------------------------------

    // 管理画面と同じく、選んだ店舗の文脈を始める (Service を直接呼ぶテスト)
    public IDisposable BeginStore(TestStore store) =>
        Services.GetRequiredService<ApplicationServiceContextProvider>().Begin(() => new ServiceContext(DateTimeOffset.UtcNow) { TenantId = store.TenantId, StoreId = store.StoreId });

    //--------------------------------------------------------------------------------
    // Visit
    //--------------------------------------------------------------------------------

    // テーブルに来店を開く (来店はスタッフが案内して開くので、テーブル端末のテストでも店舗のホール端末で開く)
    public async ValueTask<VisitResponse> OpenVisitAsync(TestStore store, int table, int adults = 2)
    {
        using var hall = new TestDevice(CreateClient());
        await hall.SignInAsync(store.HallCode);
        using var response = await hall.PostAsync("/api/v1/visits", new VisitCreateRequest { Id = Guid.CreateVersion7(), TableId = store.TableIds[table], Adults = adults });
        return await TestDevice.ReadAsync<VisitResponse>(response);
    }

    private string NextCode() => Interlocked.Increment(ref lastCode).ToString(CultureInfo.InvariantCulture);

    private static string CreateStaffPinHash()
    {
        const int iterations = 1000;
        var (salt, hash) = StaffPins.Create(StaffPin, iterations);
        return JsonSerializer.Serialize(new { iterations, salt, hash });
    }

    // DB はサーバの起動で作るので、先に起動しておく
    private async ValueTask<SqliteConnection> OpenAsync()
    {
        StartServer();
        var con = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await con.OpenAsync(TestContext.Current.CancellationToken);
        return con;
    }
}
