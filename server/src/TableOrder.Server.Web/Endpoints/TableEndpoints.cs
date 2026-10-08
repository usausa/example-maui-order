namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Stores;

public static class TableEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapTableEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Tables);

        group.MapGet(string.Empty, HandleListAsync)
            .RequireAuthorization(Policies.TableReader)
            .WithName("TableList")
            .Produces<TableListResponse>()
            .ProducesValidationProblem();
    }

    //--------------------------------------------------------------------------------
    // List
    //--------------------------------------------------------------------------------

    // status (Vacant / Occupied / Paying) は文字のまま渡す (読めない値も Problem Details の 400 にする)
    private static async ValueTask<IResult> HandleListAsync(
        StoreService storeService,
        string? status,
        CancellationToken cancellationToken) =>
        ApiResults.Ok(await storeService.GetTablesAsync(status, cancellationToken));
}
