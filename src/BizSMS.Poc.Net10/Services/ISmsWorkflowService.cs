using BizSMS.Poc.Net10.Models;

namespace BizSMS.Poc.Net10.Services;

public interface ISmsWorkflowService
{
    Task<SmsExecutionResult> ExecuteAsync(ApplicationUser user, PendingSendCommand command, CancellationToken ct = default);
}
