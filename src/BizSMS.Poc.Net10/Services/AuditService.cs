using BizSMS.Poc.Net10.Data;
using BizSMS.Poc.Net10.Models;

namespace BizSMS.Poc.Net10.Services;

public sealed class AuditService : IAuditService
{
    private readonly BizSmsDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditService(BizSmsDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task LogAsync(string source, string message, string? user = null, string? exception = null, CancellationToken ct = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var controller = httpContext?.GetRouteValue("controller")?.ToString();
        var action = httpContext?.GetRouteValue("action")?.ToString();
        var correlationId = httpContext?.TraceIdentifier;

        _db.Logs.Add(new LogModel
        {
            LogDate = DateTime.UtcNow,
            LogLevel = exception is null ? "INFO" : "ERROR",
            LogSource = source,
            User = user,
            Controller = controller,
            Action = action,
            LogMessage = $"[{correlationId}] {message}",
            Exception = exception
        });

        await _db.SaveChangesAsync(ct);
    }
}
