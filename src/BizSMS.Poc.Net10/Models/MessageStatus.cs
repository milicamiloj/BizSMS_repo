namespace BizSMS.Poc.Net10.Models;

public enum MessageStatus
{
    Queued = 1,
    Scheduled = 2,
    Processing = 3,
    Finished = 4,
    ScheduledSendingCanceled = 5
}
