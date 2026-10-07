namespace TableOrder.Server.Web.Application;

using TableOrder.Server.Web.Application.Context;
using TableOrder.Server.Web.Application.Telemetry;

public static class EndpointExtensions
{
    // API のグループは既定で認証を求める (匿名で受ける入口は AllowAnonymous で外す)
    public static RouteGroupBuilder MapApiGroup(this IEndpointRouteBuilder endpoints, string prefix) =>
        endpoints.MapGroup(prefix)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .AddEndpointFilter<RequestMetricsEndpointFilter>()
            .AddEndpointFilter<ServiceContextEndpointFilter>();
}
