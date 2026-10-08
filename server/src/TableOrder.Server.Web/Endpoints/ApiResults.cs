namespace TableOrder.Server.Web.Endpoints;

// 業務の処理の結果を応答にする (失敗は ApiProblems)
public static class ApiResults
{
    public static IResult Ok<T>(ServiceResult<T> result)
        where T : class =>
        result.Succeeded ? TypedResults.Ok(result.Value) : ApiProblems.From(result.Error);

    // 新しく作ったものは 201、同じ Id の送り直しで既にあったものは 200
    public static IResult Created<T>(ServiceResult<T> result, Func<T, string> location)
        where T : class
    {
        if (!result.Succeeded)
        {
            return ApiProblems.From(result.Error);
        }

        return result.Created ? TypedResults.Created(new Uri(location(result.Value), UriKind.Relative), result.Value) : TypedResults.Ok(result.Value);
    }

    public static IResult NoContent(ServiceError? error) =>
        error is null ? TypedResults.NoContent() : ApiProblems.From(error);

    // 画像は内容が変わると名前が変わるので、端末に長く持たせる
    public static IResult Image(HttpContext context, ServiceResult<ImageResult> result)
    {
        if (!result.Succeeded)
        {
            return ApiProblems.From(result.Error);
        }

        context.Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return TypedResults.Bytes(result.Value.Content, result.Value.ContentType);
    }
}
