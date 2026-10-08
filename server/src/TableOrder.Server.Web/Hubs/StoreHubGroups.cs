namespace TableOrder.Server.Web.Hubs;

// ハブのグループの名前。持ち場の Id はメニューごとに決まり、ほかのテナント・店舗と重なりうるので、テナントと店舗を必ず入れる
public static class StoreHubGroups
{
    // 端末の入るグループ (種類と、テーブル端末はテーブル、キッチン端末は持ち場)
    public static IEnumerable<string> Of(ServiceContext context)
    {
        if ((context.TenantId is not { } tenantId) || (context.StoreId is not { } storeId) || (context.DeviceKind is not { } kind))
        {
            yield break;
        }

        yield return Kind(tenantId, storeId, kind);
        if ((kind == DeviceKind.Table) && (context.TableId is { } tableId))
        {
            yield return Table(tenantId, storeId, tableId);
        }

        if (kind == DeviceKind.Kitchen)
        {
            foreach (var stationId in context.StationIds)
            {
                yield return Station(tenantId, storeId, stationId);
            }
        }
    }

    // 通知を送るグループ。1 つの端末が 2 つのグループで同じ通知を受けないように、種類で送るときはテーブル・持ち場のグループに送らない
    public static IReadOnlyList<string> For(Guid tenantId, Guid storeId, EventDelivery delivery)
    {
        var route = delivery.Route;
        var groups = route.Kinds.Select(x => Kind(tenantId, storeId, x)).ToList();
        if (route.Tables && !route.Kinds.Contains(DeviceKind.Table))
        {
            if (delivery.TableIds is null)
            {
                groups.Add(Kind(tenantId, storeId, DeviceKind.Table));
            }
            else
            {
                groups.AddRange(delivery.TableIds.Distinct().Select(x => Table(tenantId, storeId, x)));
            }
        }

        if (route.Stations && !route.Kinds.Contains(DeviceKind.Kitchen))
        {
            groups.Add(delivery.StationId is { } stationId ? Station(tenantId, storeId, stationId) : Kind(tenantId, storeId, DeviceKind.Kitchen));
        }

        return groups;
    }

    private static string Kind(Guid tenantId, Guid storeId, DeviceKind kind) => $"{tenantId:N}:{storeId:N}:kind:{kind}";

    private static string Table(Guid tenantId, Guid storeId, Guid tableId) => $"{tenantId:N}:{storeId:N}:table:{tableId:N}";

    private static string Station(Guid tenantId, Guid storeId, Guid stationId) => $"{tenantId:N}:{storeId:N}:station:{stationId:N}";
}
