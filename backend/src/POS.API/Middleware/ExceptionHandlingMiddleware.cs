using System.Text.Json;
using POS.Application.Common;

namespace POS.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var statusCode = ex switch
            {
                NotFoundException => StatusCodes.Status404NotFound,
                ValidationException => StatusCodes.Status400BadRequest,
                UnauthorizedException => StatusCodes.Status401Unauthorized,
                _ => StatusCodes.Status500InternalServerError,
            };

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = statusCode;

            var message = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : ex.Message;

            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = message }));
        }
    }
}
