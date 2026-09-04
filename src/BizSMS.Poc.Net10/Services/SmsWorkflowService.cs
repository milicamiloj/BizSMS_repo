using BizSMS.Poc.Net10.Data;
using BizSMS.Poc.Net10.Models;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Poc.Net10.Services;

public sealed class SmsWorkflowService : ISmsWorkflowService
{
    private readonly BizSmsDbContext _db;
    private readonly IAuditService _audit;

    public SmsWorkflowService(BizSmsDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<SmsExecutionResult> ExecuteAsync(ApplicationUser user, PendingSendCommand command, CancellationToken ct = default)
    {
        var number = await _db.Numbers
            .Where(n => n.NumberID == command.NumberId
                        && n.ClientID == user.ClientID
                        && n.Active
                        && n.SendAllowed)
            .SingleOrDefaultAsync(ct);

        if (number is null)
        {
            throw new InvalidOperationException("Broj ne postoji ili slanje nije dozvoljeno.");
        }

        var now = DateTime.UtcNow;
        var sendDate = command.ScheduledAtUtc ?? now;
        var scheduled = sendDate > now;

        var text = command.MessageText.Trim();
        if (number.NumberTypeID != 1)
        {
            var stopId = user.ClientID.ToString("00000");
            text = $"{text}{Environment.NewLine}STOP {stopId}";
        }

        var message = new MessageModel
        {
            Sender = "BizSMS",
            MessageText = text,
            MessageLength = GetMessageLength(text),
            SendDate = sendDate,
            InsertDate = now,
            Test = false,
            Status = (int)(scheduled ? MessageStatus.Scheduled : MessageStatus.Finished),
            Charged = true,
            UserID = user.Id
        };

        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);

        if (scheduled)
        {
            _db.ScheduledSms.Add(new ScheduledSmsModel
            {
                HangfireID = $"POC-{Guid.NewGuid():N}",
                MessageID = message.MessageID,
                UserInsert = user.Id,
                InsertDate = now
            });
            await _db.SaveChangesAsync(ct);
        }

        await _audit.LogAsync(
            source: "SMS_SEND_OR_SCHEDULE",
            message: $"MessageId={message.MessageID}, NumberId={number.NumberID}, Scheduled={scheduled}",
            user: user.UserName,
            ct: ct);

        return new SmsExecutionResult
        {
            MessageId = message.MessageID,
            Scheduled = scheduled,
            SendDateUtc = sendDate
        };
    }

    private static int GetMessageLength(string message)
    {
        return message.Any(ch => ch > 127)
            ? (int)Math.Ceiling(message.Length / 66d)
            : (int)Math.Ceiling(message.Length / 160d);
    }
}
