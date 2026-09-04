namespace BizSMS.Poc.Net10.Models;

public sealed class SmsExecutionResult
{
    public int MessageId { get; init; }
    public bool Scheduled { get; init; }
    public DateTime SendDateUtc { get; init; }
}
