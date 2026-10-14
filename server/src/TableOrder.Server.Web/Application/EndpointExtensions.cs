namespace TableOrder.Server.Web.Application;

using TableOrder.Server.Web.Application.Context;
using TableOrder.Server.Web.Application.Telemetry;

public static class EndpointExtensions
{
    // API のグループは既定で認証を求め、401 と 500 の応答を宣言する (匿名で受ける入口は AllowAnonymous で外し、宣言は引き継ぐ)
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder endpoints, string prefix) =>
        endpoints.MapGroup(prefix)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .AddEndpointFilter<RequestMetricsEndpointFilter>()
            .AddEndpointFilter<ServiceContextEndpointFilter>();
}
