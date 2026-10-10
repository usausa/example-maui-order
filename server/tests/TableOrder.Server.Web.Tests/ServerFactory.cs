namespace TableOrder.Server.Web;

using System.Globalization;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using TableOrder.Contract.Menu;
using TableOrder.Contract.Visits;
using TableOrder.Server.Core.Infrastructure.Data;
using TableOrder.Server.Core.Infrastructure.Json;
using TableOrder.Server.Core.Models.Entity;
using TableOrder.Server.Core.Models.Enums;
using TableOrder.Server.Core.Services;
using TableOrder.Server.Web.Application.Authentication;
using TableOrder.Server.Web.Application.Context;

// テストのクラスごとのサーバ。一時ファイルの DB に、サンプルのデータ (2 つのテナント) を入れて起動する
public sealed class ServerFactory : WebApplicationFactory<Program>
{
    private const string Now = "2026-01-01 00:00:00.0000000";

    // テストの店舗のスタッフの PIN (ハッシュは確かめが速いように回数を少なくする)
    public const string StaffPin = "1234";

    // 管理画面のテストの利用者のパスワード
    public const string AdminPassword = "test-admin-password";

    private static readonly string StaffPinHash = CreateStaffPinHash();

    // サンプルのデータのコード (1xxxxx、2xxxxx) と重ならないペアリングコード
    // コードの端末の種類を TestCodes で引くので、テストのサーバをまたいで重ならないように数える
    private static int lastCode = 300_000;

    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"tableorder-test-{Guid.NewGuid():N}.db");

    // 画像の置き場 (クラスごとの一時のフォルダ。サンプルの写真を起動で写す)
    private readonly string imagePath = Path.Combine(Path.GetTempPath(), $"tableorder-test-images-{Guid.NewGuid():N}");

    // 管理画面のテストの利用者のメールアドレスの通し番号
    private int lastUser;

    // テストで替える設定 (起動の前に入れる。既定の設定より後に読む)
    public Dictionary<string, string?> Settings { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={databasePath};Pooling=False;Foreign Keys=True",
                ["Database:SampleData"] = "true",
                ["Image:Directory"] = imagePath,
                ["RateLimit:PairingPerMinute"] = "10000",
                ["RateLimit:SignInPerMinute"] = "10000",
                ["Log:HttpLog"] = "false",
                ["Simulation:Enabled"] = "false",
                ["Serilog:MinimumLevel:Default"] = "Warning"
            })
            .AddInMemoryCollection(Settings));
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
    // Arrange (管理画面の操作の結果を、版と文脈なしで DB に直接書く)
    //--------------------------------------------------------------------------------

    // 店舗のラストオーダーの時刻 (開店と同じ時刻にすると、いつでも過ぎている)
    public async ValueTask SetLastOrderTimeAsync(TestStore store, string? lastOrderTime)
    {
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = "UPDATE Stores SET LastOrderTime = @lastOrderTime WHERE TenantId = @tenantId AND Id = @storeId";
        command.Parameters.AddWithValue("@tenantId", store.TenantId.ToString("D"));
        command.Parameters.AddWithValue("@storeId", store.StoreId.ToString("D"));
        command.Parameters.AddWithValue("@lastOrderTime", (object?)lastOrderTime ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // 店舗の時間帯の終わりの猶予の分数
    public async ValueTask SetDaypartGraceAsync(TestStore store, int minutes)
    {
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = "UPDATE Stores SET Features = json_set(Features, '$.daypartGraceMinutes', @minutes) WHERE TenantId = @tenantId AND Id = @storeId";
        command.Parameters.AddWithValue("@tenantId", store.TenantId.ToString("D"));
        command.Parameters.AddWithValue("@storeId", store.StoreId.ToString("D"));
        command.Parameters.AddWithValue("@minutes", minutes);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // サンプルのメニューを直して店舗に公開し、公開の Id を返す (時間帯や出せる条件のルールを持つメニューの代わり。版は新しくし、公開の時刻は今にする)
    public async ValueTask<Guid> PublishMenuAsync(TestStore store, Action<MenuResponse> edit)
    {
        await using var con = await OpenAsync();
        MenuResponse menu;
        await using (var read = con.CreateCommand())
        {
            read.CommandText = "SELECT Content FROM MenuPublications WHERE Id = '00000000-0000-0000-0009-000000000001'";
            menu = JsonSerializer.Deserialize<MenuResponse>((string)(await read.ExecuteScalarAsync(TestContext.Current.CancellationToken))!, JsonDefaults.Options)!;
        }

        var publicationId = Guid.CreateVersion7();
        menu.MenuVersion = publicationId.ToString("N");
        edit(menu);

        await using var command = con.CreateCommand();
        command.CommandText = """
            INSERT INTO MenuPublications (TenantId, Id, StoreId, MenuVersion, Content, PublishedAt)
                VALUES (@tenantId, @publicationId, @storeId, @menuVersion, @content, @now);
            UPDATE Stores SET MenuPublicationId = @publicationId WHERE TenantId = @tenantId AND Id = @storeId;
            """;
        command.Parameters.AddWithValue("@tenantId", store.TenantId.ToString("D"));
        command.Parameters.AddWithValue("@storeId", store.StoreId.ToString("D"));
        command.Parameters.AddWithValue("@publicationId", publicationId.ToString("D"));
        command.Parameters.AddWithValue("@menuVersion", menu.MenuVersion);
        command.Parameters.AddWithValue("@content", JsonSerializer.Serialize(menu, JsonDefaults.Options));
        command.Parameters.AddWithValue("@now", DateTimeOffsetTextConverter.ToDb(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        return publicationId;
    }

    // 店舗の今のメニューを、前に公開したものに替える
    public async ValueTask UseMenuPublicationAsync(TestStore store, Guid publicationId)
    {
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = "UPDATE Stores SET MenuPublicationId = @publicationId WHERE TenantId = @tenantId AND Id = @storeId";
        command.Parameters.AddWithValue("@tenantId", store.TenantId.ToString("D"));
        command.Parameters.AddWithValue("@storeId", store.StoreId.ToString("D"));
        command.Parameters.AddWithValue("@publicationId", publicationId.ToString("D"));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // 店舗のメニューの公開の Id
    public async ValueTask<List<Guid>> QueryMenuPublicationIdsAsync(TestStore store)
    {
        var ids = new List<Guid>();
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = "SELECT Id FROM MenuPublications WHERE TenantId = @tenantId AND StoreId = @storeId";
        command.Parameters.AddWithValue("@tenantId", store.TenantId.ToString("D"));
        command.Parameters.AddWithValue("@storeId", store.StoreId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            ids.Add(Guid.Parse(reader.GetString(0)));
        }

        return ids;
    }

    // 明細の税率 (税率を直したメニューの前に受けた明細の代わり)
    public async ValueTask SetLineTaxRateAsync(TestStore store, Guid lineId, decimal taxRate)
    {
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = "UPDATE OrderLines SET TaxRate = @taxRate WHERE TenantId = @tenantId AND Id = @lineId";
        command.Parameters.AddWithValue("@tenantId", store.TenantId.ToString("D"));
        command.Parameters.AddWithValue("@lineId", lineId.ToString("D"));
        command.Parameters.AddWithValue("@taxRate", taxRate);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // 来店の営業日を days 日前にする (前の営業日の来店や、残す期間を過ぎた来店の代わり)
    public async ValueTask MoveVisitToPreviousDayAsync(TestStore store, Guid visitId, int days = 1)
    {
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = "UPDATE Visits SET BusinessDate = date(BusinessDate, @modifier) WHERE TenantId = @tenantId AND Id = @visitId";
        command.Parameters.AddWithValue("@tenantId", store.TenantId.ToString("D"));
        command.Parameters.AddWithValue("@visitId", visitId.ToString("D"));
        command.Parameters.AddWithValue("@modifier", $"-{days} days");
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // 来店とその子の表の行の数 (表ごと。古いデータの片付けで消えたかを数える)
    public async ValueTask<Dictionary<string, long>> CountVisitRowsAsync(TestStore store, Guid visitId)
    {
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = """
            SELECT 'Visits', COUNT(*) FROM Visits WHERE TenantId = @tenantId AND Id = @visitId
            UNION ALL SELECT 'VisitConfirmations', COUNT(*) FROM VisitConfirmations WHERE TenantId = @tenantId AND VisitId = @visitId
            UNION ALL SELECT 'Orders', COUNT(*) FROM Orders WHERE TenantId = @tenantId AND VisitId = @visitId
            UNION ALL SELECT 'OrderLines', COUNT(*) FROM OrderLines WHERE TenantId = @tenantId AND VisitId = @visitId
            UNION ALL SELECT 'OrderLineOptions', COUNT(*) FROM OrderLineOptions WHERE TenantId = @tenantId AND LineId IN (SELECT Id FROM OrderLines WHERE TenantId = @tenantId AND VisitId = @visitId)
            UNION ALL SELECT 'KitchenTickets', COUNT(*) FROM KitchenTickets WHERE TenantId = @tenantId AND VisitId = @visitId
            UNION ALL SELECT 'Calls', COUNT(*) FROM Calls WHERE TenantId = @tenantId AND VisitId = @visitId
            UNION ALL SELECT 'Payments', COUNT(*) FROM Payments WHERE TenantId = @tenantId AND VisitId = @visitId
            UNION ALL SELECT 'Receipts', COUNT(*) FROM Receipts WHERE TenantId = @tenantId AND VisitId = @visitId
            """;
        command.Parameters.AddWithValue("@tenantId", store.TenantId.ToString("D"));
        command.Parameters.AddWithValue("@visitId", visitId.ToString("D"));
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            counts[reader.GetString(0)] = reader.GetInt64(1);
        }

        return counts;
    }

    // ほかのサーバが書いた通知 (このサーバの送り手に知らせずに、DB にだけ書く)。中身は空で、店舗のすべての端末に送る
    public async ValueTask WriteEventElsewhereAsync(TestStore store, string type)
    {
        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = """
            INSERT INTO EventSequences (TenantId, StoreId, LastSeq) VALUES (@tenantId, @storeId, 0) ON CONFLICT (TenantId, StoreId) DO NOTHING;
            UPDATE EventSequences SET LastSeq = LastSeq + 1 WHERE TenantId = @tenantId AND StoreId = @storeId;
            INSERT INTO Events (TenantId, StoreId, Seq, Type, OccurredAt, Data, TableIds, StationId)
                SELECT TenantId, StoreId, LastSeq, @type, @now, '{}', NULL, NULL FROM EventSequences WHERE TenantId = @tenantId AND StoreId = @storeId;
            """;
        command.Parameters.AddWithValue("@tenantId", store.TenantId.ToString("D"));
        command.Parameters.AddWithValue("@storeId", store.StoreId.ToString("D"));
        command.Parameters.AddWithValue("@type", type);
        command.Parameters.AddWithValue("@now", DateTimeOffsetTextConverter.ToDb(DateTimeOffset.UtcNow));
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // 端末を無効にし、管理画面の操作と同じく、すぐに拒む一覧を読み直す (refresh を外すと、ほかのサーバで無効にしたときのように読み直さない)
    public async ValueTask RevokeDeviceAsync(Guid deviceId, bool refresh = true)
    {
        await using (var con = await OpenAsync())
        {
            await using var command = con.CreateCommand();
            command.CommandText = "UPDATE Devices SET IsActive = 0, RevokedAt = @now WHERE Id = @id";
            command.Parameters.AddWithValue("@id", deviceId.ToString("D"));
            command.Parameters.AddWithValue("@now", DateTimeOffsetTextConverter.ToDb(DateTimeOffset.UtcNow));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        if (refresh)
        {
            await Services.GetRequiredService<RevocationList>().RefreshAsync(TestContext.Current.CancellationToken);
        }
    }

    // テナントを止め、管理画面の操作と同じく、すぐに拒む一覧を読み直す
    public async ValueTask SuspendTenantAsync(Guid tenantId)
    {
        await using (var con = await OpenAsync())
        {
            await using var command = con.CreateCommand();
            command.CommandText = "UPDATE Tenants SET Status = 'Suspended' WHERE Id = @id";
            command.Parameters.AddWithValue("@id", tenantId.ToString("D"));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await Services.GetRequiredService<RevocationList>().RefreshAsync(TestContext.Current.CancellationToken);
    }

    // 止めたテナントを戻し、管理画面の操作と同じく、すぐに拒む一覧を読み直す
    public async ValueTask ResumeTenantAsync(Guid tenantId)
    {
        await using (var con = await OpenAsync())
        {
            await using var command = con.CreateCommand();
            command.CommandText = "UPDATE Tenants SET Status = 'Active' WHERE Id = @id";
            command.Parameters.AddWithValue("@id", tenantId.ToString("D"));
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await Services.GetRequiredService<RevocationList>().RefreshAsync(TestContext.Current.CancellationToken);
    }

    // ほかのテストに関わらないテナント (店舗と、ホール端末のペアリングコード) を作る
    public async ValueTask<(Guid TenantId, string PairingCode)> CreateTenantAsync()
    {
        var tenantId = Guid.CreateVersion7();
        var code = NextCode(DeviceKind.Hall);

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

    // ほかのテストに関わらない店舗。テーブルを 3 つ (定員は 4、4、6) 置き、今のメニューはデモの店舗と同じにして、呼び出しの用件 (Staff、Water) と端末の種類ごとのペアリングコードを出す
    public async ValueTask<TestStore> CreateStoreAsync(VisitOpening visitOpening = VisitOpening.Hall)
    {
        var tenantId = Guid.CreateVersion7();
        var storeId = Guid.CreateVersion7();
        var publicationId = Guid.CreateVersion7();
        var tableIds = new[] { Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7() };
        var tableCodes = tableIds.Select(static _ => NextCode(DeviceKind.Table)).ToArray();
        var hallCode = NextCode(DeviceKind.Hall);
        var kitchenCode = NextCode(DeviceKind.Kitchen);
        var receptionCode = NextCode(DeviceKind.Reception);

        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = """
            BEGIN;
            INSERT INTO Tenants (Id, Code, Name, BrandName, Status, CreatedAt, UpdatedAt, Version)
                VALUES (@tenantId, @tenantCode, 'test', '{"ja":"チェーン"}', 'Active', @now, @now, 1);
            INSERT INTO Stores (TenantId, Id, Code, Name, TimeZone, OpenTime, CloseTime, OrderingPaused, TaxRounding, MaxQuantityPerLine, MaxLinesPerOrder, Languages, PaymentMethods, ElectronicReceipt, Features, StaffPinHash, SettingsVersion, MenuPublicationId, IsActive, CreatedAt, UpdatedAt, Version)
                VALUES (@tenantId, @storeId, '001', '{"ja":"店"}', 'Asia/Tokyo', '05:00', '04:00', 0, 'Floor', 9, 20, '["ja"]', '["QrCode","CreditCard"]', 1, @features, @staffPinHash, 1, @publicationId, 1, @now, @now, 1);
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
        command.Parameters.AddWithValue("@features", $$"""{"visitOpening":"{{visitOpening}}"}""");
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

    // ほかのテストに関わらない管理画面の利用者 (パスワードは AdminPassword)。メールアドレスを返す
    public async ValueTask<string> CreateAdminUserAsync(AdminRole role, Guid? tenantId, IReadOnlyList<Guid>? storeIds = null, bool mustChangePassword = false, bool isActive = true)
    {
        StartServer();
        var userId = Guid.CreateVersion7();
        var email = $"user{Interlocked.Increment(ref lastUser)}@admin.example.com";
        var hash = new PasswordHasher<AdminUserEntity>().HashPassword(new AdminUserEntity(), AdminPassword);

        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = """
            INSERT INTO AdminUsers (Id, TenantId, Role, Email, NormalizedEmail, Name, PasswordHash, MustChangePassword, SecurityStamp, AccessFailedCount, TwoFactorEnabled, IsActive, CreatedAt, UpdatedAt, Version)
                VALUES (@id, @tenantId, @role, @email, @normalizedEmail, 'test', @hash, @mustChangePassword, @stamp, 0, 0, @isActive, @now, @now, 1);
            """;
        command.Parameters.AddWithValue("@id", userId.ToString("D"));
        command.Parameters.AddWithValue("@tenantId", (object?)tenantId?.ToString("D") ?? DBNull.Value);
        command.Parameters.AddWithValue("@role", role.ToString());
        command.Parameters.AddWithValue("@email", email);
        command.Parameters.AddWithValue("@normalizedEmail", email.ToUpperInvariant());
        command.Parameters.AddWithValue("@hash", hash);
        command.Parameters.AddWithValue("@mustChangePassword", mustChangePassword ? 1 : 0);
        command.Parameters.AddWithValue("@stamp", Guid.NewGuid().ToString("N"));
        command.Parameters.AddWithValue("@isActive", isActive ? 1 : 0);
        command.Parameters.AddWithValue("@now", Now);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        foreach (var storeId in storeIds ?? [])
        {
            await using var store = con.CreateCommand();
            store.CommandText = "INSERT INTO AdminUserStores (TenantId, UserId, StoreId) VALUES (@tenantId, @userId, @storeId)";
            store.Parameters.AddWithValue("@tenantId", tenantId!.Value.ToString("D"));
            store.Parameters.AddWithValue("@userId", userId.ToString("D"));
            store.Parameters.AddWithValue("@storeId", storeId.ToString("D"));
            await store.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        return email;
    }

    //--------------------------------------------------------------------------------
    // Context
    //--------------------------------------------------------------------------------

    // 管理画面と同じく、選んだ店舗の文脈を始める (Service を直接呼ぶテスト)
    public IDisposable BeginStore(TestStore store) =>
        Services.GetRequiredService<ApplicationServiceContextProvider>().Begin(() => new ServiceContext(DateTimeOffset.UtcNow) { TenantId = store.TenantId, StoreId = store.StoreId });

    // 管理画面と同じく、選んだテナントの文脈を始める (店舗を選ばない画面。テナントの利用者の管理)
    public IDisposable BeginTenant(Guid? tenantId) =>
        Services.GetRequiredService<ApplicationServiceContextProvider>().Begin(() => new ServiceContext(DateTimeOffset.UtcNow) { TenantId = tenantId });

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

    private static string NextCode(DeviceKind kind)
    {
        var code = Interlocked.Increment(ref lastCode).ToString(CultureInfo.InvariantCulture);
        TestCodes.Add(code, kind);
        return code;
    }

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
