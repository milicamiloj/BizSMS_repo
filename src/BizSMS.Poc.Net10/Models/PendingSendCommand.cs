namespace BizSMS.Poc.Net10.Models;

public sealed class PendingSendCommand
{
    public string ScopeId { get; init; } = Guid.NewGuid().ToString("N");
    public int NumberId { get; init; }
    public string MessageText { get; init; } = string.Empty;
    public DateTime? ScheduledAtUtc { get; init; }
}
