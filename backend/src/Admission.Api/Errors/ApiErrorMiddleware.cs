using Microsoft.AspNetCore.Http;

namespace Admission.Api.Errors;

public sealed record ApiError(string Code, string Message)
{
    public static IResult Result(int statusCode, string code, string message) =>
        Results.Json(new ApiError(code, message), statusCode: statusCode);
}

/// <summary>
/// The one place that turns failures into the JSON error body: thrown exceptions,
/// binding failures, and framework responses that have no body, such as 404 and 405.
/// </summary>
public sealed class ApiErrorMiddleware(RequestDelegate next, ILogger<ApiErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BadHttpRequestException ex) when (!context.Response.HasStarted)
        {
            var (code, message) = ex.StatusCode == StatusCodes.Status400BadRequest
                ? ("INVALID_JSON", "The request body must be valid JSON with fields of the correct type.")
                : DescribeStatus(ex.StatusCode);
            await WriteAsync(context, ex.StatusCode, code, message);
            return;
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "Something went wrong on the server. Please try again.");
            return;
        }

        var response = context.Response;
        if (response.StatusCode >= 400
            && !response.HasStarted
            && response.ContentLength is null
            && string.IsNullOrEmpty(response.ContentType))
        {
            var (code, message) = DescribeStatus(response.StatusCode);
            await response.WriteAsJsonAsync(new ApiError(code, message));
        }
    }

    private static (string Code, string Message) DescribeStatus(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ("BAD_REQUEST", "The request is not valid."),
        StatusCodes.Status404NotFound => ("NOT_FOUND", "The requested resource was not found."),
        StatusCodes.Status405MethodNotAllowed => ("METHOD_NOT_ALLOWED", "This method is not allowed for this resource."),
        StatusCodes.Status413PayloadTooLarge => ("PAYLOAD_TOO_LARGE", "The request body is too large."),
        StatusCodes.Status415UnsupportedMediaType => ("UNSUPPORTED_MEDIA_TYPE", "The request body must be sent as application/json."),
        _ => ($"HTTP_{statusCode}", "The request could not be completed."),
    };

    private static async Task WriteAsync(HttpContext context, int statusCode, string code, string message)
    {
        // Clear does not remove OnStarting callbacks, so CORS headers still apply.
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(new ApiError(code, message));
    }
}
