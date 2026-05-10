using Microsoft.AspNetCore.Diagnostics;
using TaskFlow.Api;

namespace TaskFlow.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled application exception");

            if (context.Request.Path.StartsWithSegments("/api"))
            {
                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(ApiResponse<object>.Fail("Внутренняя ошибка сервера."));
                return;
            }

            context.Features.Set<IExceptionHandlerFeature>(new ExceptionHandlerFeature { Error = exception });
            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Request.Path = "/Home/Error";
            await _next(context);
        }
    }
}
