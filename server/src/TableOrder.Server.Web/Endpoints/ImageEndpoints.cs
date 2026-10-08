namespace TableOrder.Server.Web.Endpoints;

public static class ImageEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapImageEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Images);

        group.MapGet("/{name}", HandleGetAsync)
            .RequireAuthorization(Policies.AnyDevice)
            .WithName("ImageGet")
            .Produces(StatusCodes.Status200OK, responseType: null, contentType: "image/png", additionalContentTypes: ["image/jpeg", "image/webp"])
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    //--------------------------------------------------------------------------------
    // Get
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleGetAsync(
        HttpContext context,
        ImageService imageService,
        string name,
        CancellationToken cancellationToken) =>
        ApiResults.Image(context, await imageService.GetAsync(name, cancellationToken));
}
