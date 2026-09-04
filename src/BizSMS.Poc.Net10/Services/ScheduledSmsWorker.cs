using BizSMS.Poc.Net10.Data;
using BizSMS.Poc.Net10.Models;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Poc.Net10.Services;

public sealed class ScheduledSmsWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledSmsWorker> _logger;

    public ScheduledSmsWorker(IServiceScopeFactory scopeFactory, ILogger<ScheduledSmsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ScheduledSmsWorker failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessDueMessages(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BizSmsDbContext>();
        var audit = scope.ServiceProvider.GetRequiredService<IAuditService>();

        var due = await db.Messages
            .Where(m => m.Status == (int)MessageStatus.Scheduled && m.SendDate <= DateTime.UtcNow)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in due)
        {
            var claimed = await db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE BST_MESSAGES
SET Status = {(int)MessageStatus.Processing}
WHERE Message_ID = {msg.MessageID}
  AND Status = {(int)MessageStatus.Scheduled}", ct);

            if (claimed == 0)
            {
                continue;
            }

            msg.Status = (int)MessageStatus.Finished;
            await db.SaveChangesAsync(ct);

            await audit.LogAsync(
                "SCHEDULED_SMS_EXECUTED",
                $"Scheduled message executed. MessageId={msg.MessageID}",
                msg.UserID,
                ct: ct);
        }
    }
}
