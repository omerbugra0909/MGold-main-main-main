using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using MGold.Application.DTOs;
using MGold.Application.Exceptions;
using MGold.Application.Interfaces;

namespace MGold.Middleware;

public class AuditLogActionFilter(
    IAuditLogService auditLogService,
    ICurrentUserService currentUserService,
    ILogger<AuditLogActionFilter> logger) : IAsyncActionFilter
{
    private static readonly HashSet<string> TrackedMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        HttpMethods.Post,
        HttpMethods.Put,
        HttpMethods.Patch,
        HttpMethods.Delete
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var method = context.HttpContext.Request.Method;
        if (!TrackedMethods.Contains(method))
        {
            await next();
            return;
        }

        var beforeState = SerializeAndMask(context.ActionArguments);
        var entityName = ResolveEntityName(context);
        var actionType = ResolveActionType(method, context);
        var entityId = context.RouteData.Values.TryGetValue("id", out var id) ? id?.ToString() : null;

        var executedContext = await next();
        var statusCode = context.HttpContext.Response.StatusCode;
        if (executedContext.Exception is not null)
        {
            statusCode = executedContext.Exception switch
            {
                AuthorizationException => StatusCodes.Status403Forbidden,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                BusinessRuleException => StatusCodes.Status400BadRequest,
                _ => StatusCodes.Status500InternalServerError
            };
        }
        var isSuccess = executedContext.Exception is null && statusCode is >= 200 and < 400;

        var afterState = executedContext.Result switch
        {
            ObjectResult objectResult => SerializeAndMask(objectResult.Value),
            _ => null
        };

        var errorMessage = executedContext.Exception?.Message;

        try
        {
            await auditLogService.WriteAsync(new CreateAuditLogDto
            {
                UserId = currentUserService.UserId,
                Username = currentUserService.Username,
                UserRole = currentUserService.Role,
                ActionType = actionType,
                EntityName = entityName,
                EntityId = entityId,
                HttpMethod = method,
                Path = context.HttpContext.Request.Path.Value ?? string.Empty,
                CorrelationId = RequestCorrelationMiddleware.GetCorrelationId(context.HttpContext),
                IsSuccess = isSuccess,
                StatusCode = statusCode,
                BeforeState = beforeState,
                AfterState = afterState,
                ErrorMessage = errorMessage
            }, context.HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            // Audit write failures should never block business operations.
            logger.LogError(ex, "Failed to write audit log for path {Path}", context.HttpContext.Request.Path);
        }
    }

    private static string ResolveEntityName(ActionExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            return descriptor.ControllerName;
        }

        return "Unknown";
    }

    private static string ResolveActionType(string method, ActionExecutingContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            if (descriptor.ControllerName.Equals("Auth", StringComparison.OrdinalIgnoreCase))
            {
                return descriptor.ActionName;
            }
        }

        return method.ToUpperInvariant() switch
        {
            "POST" => "Create",
            "PUT" => "Update",
            "PATCH" => "Update",
            "DELETE" => "Delete",
            _ => "Action"
        };
    }

    private static string? SerializeAndMask(object? data)
    {
        if (data is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(data);
        if (json.Length > 6000)
        {
            json = json[..6000];
        }

        json = Regex.Replace(json, "(?i)\"password\"\\s*:\\s*\"[^\"]*\"", "\"password\":\"***\"");
        json = Regex.Replace(json, "(?i)\"token\"\\s*:\\s*\"[^\"]*\"", "\"token\":\"***\"");

        return json;
    }
}
