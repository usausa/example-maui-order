namespace TableOrder.Server.Web;

using System.Globalization;
using System.Security.Cryptography;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;

// テストのクラスごとのサーバ。一時ファイルの DB に、サンプルのデータ (2 つのテナント) を入れて起動する
public sealed class ServerFactory : WebApplicationFactory<Program>
{
    private const string Now = "2026-01-01 00:00:00.0000000";

    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"tableorder-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Default"] = $"Data Source={databasePath};Pooling=False;Foreign Keys=True",
            ["Database:SampleData"] = "true",
            ["RateLimit:PairingPerMinute"] = "10000",
            ["Log:HttpLog"] = "false",
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
        var code = RandomNumberGenerator.GetInt32(300_000, 1_000_000).ToString(CultureInfo.InvariantCulture);

        await using var con = await OpenAsync();
        await using var command = con.CreateCommand();
        command.CommandText = """
            INSERT INTO Tenants (Id, Code, Name, Status, CreatedAt, UpdatedAt, Version)
                VALUES (@tenantId, @tenantCode, 'test', 'Active', @now, @now, 1);
            INSERT INTO Stores (TenantId, Id, Code, Name, TimeZone, OpenTime, CloseTime, OrderingPaused, TaxRounding, SelfStart, MaxQuantityPerLine, MaxLinesPerOrder, Languages, PaymentMethods, ElectronicReceipt, IsActive, CreatedAt, UpdatedAt, Version)
                VALUES (@tenantId, @storeId, '001', '{"ja":"店"}', 'Asia/Tokyo', '05:00', '04:00', 0, 'Floor', 1, 9, 20, '["ja"]', '[]', 0, 1, @now, @now, 1);
            INSERT INTO DeviceEnrollments (TenantId, Id, StoreId, Kind, Method, PairingCode, MaxUses, UsedCount, ExpiresAt, CreatedAt)
                VALUES (@tenantId, @enrollmentId, @storeId, 'Hall', 'PairingCode', @code, 10, 0, '9999-12-31 00:00:00.0000000', @now);
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId.ToString("D"));
        command.Parameters.AddWithValue("@tenantCode", tenantId.ToString("N"));
        command.Parameters.AddWithValue("@storeId", Guid.CreateVersion7().ToString("D"));
        command.Parameters.AddWithValue("@enrollmentId", Guid.CreateVersion7().ToString("D"));
        command.Parameters.AddWithValue("@code", code);
        command.Parameters.AddWithValue("@now", Now);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);

        return (tenantId, code);
    }

    private async ValueTask<SqliteConnection> OpenAsync()
    {
        var con = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await con.OpenAsync(TestContext.Current.CancellationToken);
        return con;
    }
}
