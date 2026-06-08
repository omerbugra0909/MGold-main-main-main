using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MGold.Application.Exceptions;
using MGold.Common;

namespace MGold.Middleware;

public class GlobalExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var correlationId = RequestCorrelationMiddleware.GetCorrelationId(context);
            logger.LogError(exception, "Unhandled exception occurred while processing request. CorrelationId={CorrelationId}", correlationId);
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = RequestCorrelationMiddleware.GetCorrelationId(context);
        var (statusCode, message) = exception switch
        {
            KeyNotFoundException => (HttpStatusCode.NotFound, exception.Message),
            BusinessRuleException => (HttpStatusCode.BadRequest, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            AuthorizationException => (HttpStatusCode.Forbidden, exception.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, exception.Message),
            SqlException => (HttpStatusCode.ServiceUnavailable, "Veritabanı bağlantısı kurulamadı. Plesk appsettings.json içindeki DefaultConnection değerini ve SQL kullanıcı şifresini kontrol edin."),
            InvalidOperationException invalid when IsDatabaseConfigurationException(invalid)
                => (HttpStatusCode.ServiceUnavailable, "Veritabanı bağlantısı yapılandırılmamış. Plesk appsettings.json içinde gerçek DefaultConnection değeri olmadan giriş, canlı market ve firma sayfaları çalışamaz."),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        if (statusCode == HttpStatusCode.InternalServerError && environment.IsDevelopment())
        {
            message = exception.Message;
        }

        context.Response.StatusCode = (int)statusCode;

        if (!IsApiRequest(context.Request))
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            var safeMessage = WebUtility.HtmlEncode(message);
            var safeCorrelationId = WebUtility.HtmlEncode(correlationId);
            await context.Response.WriteAsync($"<html><body><h2>Bir hata olustu</h2><p>{safeMessage}</p><small>CorrelationId: {safeCorrelationId}</small></body></html>");
            return;
        }

        context.Response.ContentType = "application/json";
        var response = new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Errors = [$"correlationId:{correlationId}"]
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response));
    }

    private static bool IsApiRequest(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api"))
        {
            return true;
        }

        return request.Headers.Accept.Any(x =>
            !string.IsNullOrWhiteSpace(x) &&
            x.Contains("application/json", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDatabaseConfigurationException(Exception exception)
    {
        var message = exception.Message;
        return message.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase)
            || message.Contains("connection string", StringComparison.OrdinalIgnoreCase)
            || message.Contains("SQL Server", StringComparison.OrdinalIgnoreCase)
            || exception.InnerException is not null && IsDatabaseConfigurationException(exception.InnerException);
    }
}
