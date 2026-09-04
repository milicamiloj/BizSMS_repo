using BizSMS.Poc.Net10.Data;
using BizSMS.Poc.Net10.Models;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Poc.Net10.Services;

public sealed class PocDbVerificationService : IPocDbVerificationService
{
    private readonly BizSmsDbContext _db;

    public PocDbVerificationService(BizSmsDbContext db)
    {
        _db = db;
    }

    public async Task<object> VerifyReadWriteAsync(CancellationToken ct = default)
    {
        var clientsCount = await _db.Clients.CountAsync(ct);
        var numbersCount = await _db.Numbers.CountAsync(ct);

        _db.Logs.Add(new LogModel
        {
            LogDate = DateTime.UtcNow,
            LogLevel = "INFO",
            LogSource = "POC_DB_VERIFICATION",
            LogMessage = $"Read clients={clientsCount}, numbers={numbersCount}"
        });

        await _db.SaveChangesAsync(ct);

        return new
        {
            Clients = clientsCount,
            Numbers = numbersCount,
            AuditWrite = "OK"
        };
    }
}
