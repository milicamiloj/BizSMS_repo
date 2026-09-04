using BizSMS.Poc.Net10.Models;

namespace BizSMS.Poc.Net10.Services;

public interface IActionOtpService
{
    Task RequestOtpForCommandAsync(ApplicationUser user, PendingSendCommand command, CancellationToken ct = default);
    Task<PendingSendCommand?> ConfirmAndConsumeCommandAsync(ApplicationUser user, string scopeId, string otpCode, CancellationToken ct = default);
}
