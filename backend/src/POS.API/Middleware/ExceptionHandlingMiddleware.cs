using System.Text.Json;
using POS.Application.Common;

namespace POS.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while processing {Method} {Path}", context.Request.Method, context.Request.Path);
            var statusCode = ex switch
            {
                Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
                ConflictException => StatusCodes.Status409Conflict,
                NotFoundException => StatusCodes.Status404NotFound,
                ValidationException => StatusCodes.Status400BadRequest,
                UnauthorizedException => StatusCodes.Status401Unauthorized,
                ForbiddenException => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status500InternalServerError,
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var message = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : ex is Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException
                    ? "This order, stock or shift changed. Reload and try again."
                    : ex.Message;

            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
        }
    }
}
