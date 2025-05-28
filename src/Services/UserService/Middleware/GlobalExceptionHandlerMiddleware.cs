using System.Net;
using System.Text.Json;

namespace User.Middleware;

public class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger,
    IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            // Don't log cancellations as errors, they're normal
            logger.LogInformation("Request was cancelled");
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            
            // Only return a response if headers haven't been sent yet
            if (!context.Response.HasStarted)
            {
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Request was cancelled" }));
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred");
            
            // Only return a response if headers haven't been sent yet
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";

                var response = new 
                {
                    error = env.IsDevelopment() ? ex.Message : "An error occurred. Please try again later.",
                    stackTrace = env.IsDevelopment() ? ex.StackTrace : null
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(response));
            }
        }
    }
}

// Extension method to help with registration
public static class GlobalExceptionHandlerMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    }
}
