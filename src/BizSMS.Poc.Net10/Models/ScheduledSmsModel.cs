namespace BizSMS.Poc.Net10.Models;

public sealed class ScheduledSmsModel
{
    public string HangfireID { get; set; } = string.Empty;
    public int MessageID { get; set; }
    public string UserInsert { get; set; } = string.Empty;
    public DateTime InsertDate { get; set; }
    public DateTime? CancelDate { get; set; }
    public string? UserID { get; set; }
}
