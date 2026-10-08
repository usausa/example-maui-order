namespace TableOrder.Server.Web.Endpoints;

using TableOrder.Contract.Devices;
using TableOrder.Contract.Visits;
using TableOrder.Server.Web.Application.Authentication;

public static class DeviceEndpoints
{
    //--------------------------------------------------------------------------------
    // Mapping
    //--------------------------------------------------------------------------------

    public static void MapDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapApiGroup(ApiRoutes.Devices);

        // 匿名で受ける入口。登録は接続元ごとに回数を限る
        group.MapPost("/pair", HandlePairAsync)
            .AllowAnonymous()
            .RequireRateLimiting(RateLimits.Pairing)
            .WithName("DevicePair")
            .Produces<DevicePairResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/token", HandleTokenAsync)
            .AllowAnonymous()
            .WithName("DeviceToken")
            .Produces<DeviceTokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/me/config", HandleConfigAsync)
            .RequireAuthorization(Policies.AnyDevice)
            .WithName("DeviceConfig")
            .Produces<DeviceConfigResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/me/heartbeat", HandleHeartbeatAsync)
            .RequireAuthorization(Policies.AnyDevice)
            .WithName("DeviceHeartbeat")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

        group.MapGet("/me/visit", HandleVisitAsync)
            .RequireAuthorization(Policies.TableDevice)
            .WithName("DeviceVisit")
            .Produces<VisitResponse>()
            .Produces(StatusCodes.Status204NoContent);
    }

    //--------------------------------------------------------------------------------
    // Pair
    //--------------------------------------------------------------------------------

    // 端末の登録。テナントと店舗はコードかトークンで決まり、要求に入れさせない
    private static async ValueTask<IResult> HandlePairAsync(
        DeviceService deviceService,
        DevicePairRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (String.IsNullOrEmpty(request.PairingCode) == String.IsNullOrEmpty(request.EnrollmentToken))
        {
            errors["pairingCode"] = ["ペアリングコードか登録トークンのどちらかを送ってください"];
        }

        if (String.IsNullOrWhiteSpace(request.DeviceName) || (request.DeviceName.Length > Length.DeviceName))
        {
            errors["deviceName"] = [$"端末の名前を {Length.DeviceName} 文字までで送ってください"];
        }

        if (request.AppVersion?.Length > Length.AppVersion)
        {
            errors["appVersion"] = [$"アプリの版を {Length.AppVersion} 文字までで送ってください"];
        }

        if (!DevicePublicKeys.TryCreateJwk(request.PublicKey, out var publicKey))
        {
            errors["publicKey"] = ["P-256 の公開鍵 (JWK) を送ってください"];
        }

        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors);
        }

        var result = await deviceService.PairAsync(request, publicKey!, cancellationToken);
        return result.Status switch
        {
            DevicePairStatus.Success => TypedResults.Created($"{ApiRoutes.Devices}/{result.Device!.DeviceId}", result.Device),
            DevicePairStatus.TenantSuspended => ApiProblems.TenantSuspended(),
            _ => ApiProblems.PairingCodeInvalid()
        };
    }

    //--------------------------------------------------------------------------------
    // Token
    //--------------------------------------------------------------------------------

    // アクセストークンの要求。署名を確かめるまでは、端末があるかどうかも無効かどうかも見せない (どれも 401)
    private static async ValueTask<IResult> HandleTokenAsync(
        DeviceAssertionValidator validator,
        AccessTokenService tokenService,
        DeviceService deviceService,
        DeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        if (String.IsNullOrEmpty(request.Assertion) || (DeviceAssertionValidator.ReadDeviceId(request.Assertion) is not { } deviceId))
        {
            return TypedResults.Unauthorized();
        }

        var device = await deviceService.FindAsync(deviceId, cancellationToken);
        if ((device is null) || !await validator.ValidateAsync(request.Assertion, device))
        {
            return TypedResults.Unauthorized();
        }

        var result = await deviceService.AuthorizeAsync(device, cancellationToken);
        return result.Status switch
        {
            DeviceAuthorizeStatus.Success => TypedResults.Ok(tokenService.CreateDeviceToken(result.Identity!)),
            DeviceAuthorizeStatus.TenantSuspended => ApiProblems.TenantSuspended(),
            _ => ApiProblems.DeviceRevoked()
        };
    }

    //--------------------------------------------------------------------------------
    // Me
    //--------------------------------------------------------------------------------

    private static async ValueTask<IResult> HandleConfigAsync(
        DeviceService deviceService,
        CancellationToken cancellationToken) =>
        await deviceService.GetConfigAsync(cancellationToken) is { } config ? TypedResults.Ok(config) : ApiProblems.NotFound();

    private static async ValueTask<IResult> HandleHeartbeatAsync(
        DeviceService deviceService,
        DeviceHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        if (request.BatteryLevel is < 0m or > 1m)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { ["batteryLevel"] = ["電池の残りは 0 から 1 で送ってください"] });
        }

        if (request.AppVersion?.Length > Length.AppVersion)
        {
            return ApiProblems.Validation(new Dictionary<string, string[]> { ["appVersion"] = [$"アプリの版を {Length.AppVersion} 文字までで送ってください"] });
        }

        await deviceService.ReportStatusAsync(request, cancellationToken);
        return TypedResults.NoContent();
    }

    // テーブル端末のテーブルの今の来店。なければ 204 (端末は待受にする)
    private static async ValueTask<IResult> HandleVisitAsync(
        VisitService visitService,
        CancellationToken cancellationToken) =>
        await visitService.GetCurrentAsync(cancellationToken) is { } visit ? TypedResults.Ok(visit) : TypedResults.NoContent();
}
