namespace TableOrder.Server.Web.Endpoints;

using Microsoft.Net.Http.Headers;

using TableOrder.Contract.Menu;

public static class MenuEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapMenuEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Menu);

        group.MapGet(string.Empty, HandleGetAsync)
            .RequireAuthorization(Policies.MenuReader)
            .WithName("MenuGet")
            .Produces<MenuResponse>()
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    // 公開の内容 (MenuResponse の形の JSON) をそのまま返す。menuVersion を ETag にし、変わっていなければ 304
    private static async ValueTask<IResult> HandleGetAsync(
        HttpContext context,
        MenuService menuService,
        CancellationToken cancellationToken)
    {
        if (await menuService.GetCurrentAsync(cancellationToken) is not { } menu)
        {
            return ApiProblems.NotFound();
        }

        var etag = new EntityTagHeaderValue($"\"{menu.MenuVersion}\"");
        if (context.Request.GetTypedHeaders().IfNoneMatch.Any(x => x.Compare(etag, useStrongComparison: false)))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        context.Response.GetTypedHeaders().ETag = etag;
        return TypedResults.Text(menu.Content, "application/json", Encoding.UTF8);
    }
}
