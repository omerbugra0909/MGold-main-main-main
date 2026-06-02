namespace MGold.Middleware;

public class RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    private const string ItemName = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.Items[ItemName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceIdentifier"] = context.TraceIdentifier
        }))
        {
            await next(context);
        }
    }

    public static string GetCorrelationId(HttpContext context)
        => context.Items.TryGetValue(ItemName, out var value) && value is string correlationId
            ? correlationId
            : context.TraceIdentifier;

    private static string ResolveCorrelationId(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(incoming) && incoming.Length <= 100)
        {
            return incoming.Trim();
        }

        return Guid.NewGuid().ToString("N");
    }
}
