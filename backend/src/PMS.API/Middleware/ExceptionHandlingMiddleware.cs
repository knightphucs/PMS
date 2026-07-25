using Microsoft.AspNetCore.Mvc;
using PMS.Application.Common.Exceptions;

namespace PMS.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
        => (_next, _logger, _env) = (next, logger, env);

    public async Task InvokeAsync(HttpContext context)
    {
        try { await _next(context); }
        catch (AppException ex)
        {
            _logger.LogInformation("Business exception: {Message}", ex.Message);
            await Write(context, ex.StatusCode, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception, TraceId={TraceId}", context.TraceIdentifier);
            var detail = _env.IsDevelopment() ? ex.Message : "Đã có lỗi xảy ra phía máy chủ.";
            await Write(context, 500, detail);
        }
    }

    private static async Task Write(HttpContext ctx, int status, string message)
    {
        var problem = new ProblemDetails
        {
            Status = status, Title = message,
            Extensions = { ["traceId"] = ctx.TraceIdentifier }
        };
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(problem);
    }
}