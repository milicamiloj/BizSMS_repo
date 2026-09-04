namespace BizSMS.Poc.Net10.Services;

public interface IAuditService
{
    Task LogAsync(string source, string message, string? user = null, string? exception = null, CancellationToken ct = default);
}
