using Microsoft.AspNetCore.Mvc;

namespace MGold.Common;

public static class ApiResponseFactory
{
    public static IActionResult Create<T>(ControllerBase controller, T data, string message = "Request completed successfully.", int statusCode = StatusCodes.Status200OK)
        => controller.StatusCode(statusCode, ApiResponse<T>.Ok(data, message));

    public static IActionResult CreateFailure(ControllerBase controller, string message, int statusCode, params string[] errors)
        => controller.StatusCode(statusCode, new ApiResponse<object>
        {
            Success = false,
            Message = message,
            Errors = errors
        });
}
