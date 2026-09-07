using System.Text.RegularExpressions;

namespace BizSMS.Poc.Net10.Middleware;

public sealed class CorrelationIdMiddleware
{
    private const string CorrelationHeader = "X-Correlation-ID";
    private static readonly Regex CorrelationRegex = new("^[a-zA-Z0-9-]{8,64}$", RegexOptions.Compiled);
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationHeader].FirstOrDefault();
        var correlationId = CorrelationRegex.IsMatch(incoming ?? string.Empty)
            ? incoming!
            : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[CorrelationHeader] = correlationId;

        await _next(context);
    }
}
