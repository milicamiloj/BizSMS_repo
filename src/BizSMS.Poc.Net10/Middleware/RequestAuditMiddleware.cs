using BizSMS.Poc.Net10.Services;

namespace BizSMS.Poc.Net10.Middleware;

public sealed class RequestAuditMiddleware
{
    private readonly RequestDelegate _next;

    public RequestAuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context, IAuditService auditService)
    {
        var startedAt = DateTime.UtcNow;
        try
        {
            await _next(context);
            var duration = DateTime.UtcNow - startedAt;
            await auditService.LogAsync(
                "HTTP_REQUEST",
                $"{context.Request.Method} {context.Request.Path} => {context.Response.StatusCode} in {duration.TotalMilliseconds:F0}ms",
                context.User.Identity?.Name);
        }
        catch (Exception ex)
        {
            await auditService.LogAsync(
                "HTTP_ERROR",
                $"{context.Request.Method} {context.Request.Path} failed",
                context.User.Identity?.Name,
                ex.GetType().Name);
            throw;
        }
    }
}
