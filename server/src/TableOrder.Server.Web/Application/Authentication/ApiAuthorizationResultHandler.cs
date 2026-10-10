namespace TableOrder.Server.Web.Application.Authentication;

using Microsoft.AspNetCore.Authorization.Policy;

using TableOrder.Server.Web.Endpoints;

// API の認可の結果。ポリシー (端末の種類) の範囲の外は 403 に errorCode (DEVICE_SCOPE) を付ける (置き場所の範囲は Service が確かめる)
public sealed class ApiAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler defaultHandler = new();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && context.Request.Path.StartsWithSegments(ApiRoutes.Root, StringComparison.OrdinalIgnoreCase))
        {
            return ApiProblems.DeviceScope().ExecuteAsync(context);
        }

        return defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
