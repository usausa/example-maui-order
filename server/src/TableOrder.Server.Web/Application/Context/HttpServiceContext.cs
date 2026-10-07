namespace TableOrder.Server.Web.Application.Context;

using TableOrder.Server.Web.Application.Authentication;

// 要求の文脈は、確かめたアクセストークンのクレームから作る (パス・ヘッダ・本文の値からは作らない)
public static class HttpServiceContext
{
    private const string ServiceContextKey = "__ServiceContext";

    public static ServiceContext GetOrCreate(HttpContext httpContext)
    {
        if (httpContext.Items[ServiceContextKey] is ServiceContext existing)
        {
            return existing;
        }

        var context = Create(httpContext);
        httpContext.Items[ServiceContextKey] = context;

        return context;
    }

    public static ServiceContext Create(HttpContext httpContext)
    {
        var now = httpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        var user = httpContext.User;
        if (!(user.Identity?.IsAuthenticated ?? false))
        {
            return new ServiceContext(now);
        }

        var kind = Enum.TryParse<DeviceKind>(user.FindFirstValue(ClaimNames.DeviceKind), out var value) ? value : (DeviceKind?)null;
        return new ServiceContext(now)
        {
            TenantId = ReadGuid(user.FindFirstValue(ClaimNames.TenantId)),
            StoreId = ReadGuid(user.FindFirstValue(ClaimNames.StoreId)),
            DeviceId = kind is not null ? ReadGuid(user.FindFirstValue(ClaimNames.Subject)) : null,
            DeviceKind = kind,
            TableId = ReadGuid(user.FindFirstValue(ClaimNames.TableId)),
            StationIds = user.FindAll(ClaimNames.StationIds).Select(static x => ReadGuid(x.Value)).OfType<Guid>().ToList()
        };
    }

    private static Guid? ReadGuid(string? value) =>
        Guid.TryParseExact(value, "D", out var id) ? id : null;
}
