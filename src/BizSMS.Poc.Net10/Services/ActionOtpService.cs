using BizSMS.Poc.Net10.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

namespace BizSMS.Poc.Net10.Services;

public sealed class ActionOtpService : IActionOtpService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMemoryCache _cache;
    private readonly IOtpSender _otpSender;
    private readonly IAuditService _audit;

    public ActionOtpService(
        UserManager<ApplicationUser> userManager,
        IMemoryCache cache,
        IOtpSender otpSender,
        IAuditService audit)
    {
        _userManager = userManager;
        _cache = cache;
        _otpSender = otpSender;
        _audit = audit;
    }

    public async Task RequestOtpForCommandAsync(ApplicationUser user, PendingSendCommand command, CancellationToken ct = default)
    {
        var purpose = GetPurpose(command.ScopeId);
        var otpCode = await _userManager.GenerateUserTokenAsync(user, "SendActionOtp", purpose);

        _cache.Set(GetCommandKey(user.Id, command.ScopeId), command, TimeSpan.FromMinutes(5));
        user.PhoneCodeSentAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        await _otpSender.SendAsync(user.PhoneNumber ?? string.Empty, otpCode, "SEND_OR_SCHEDULE", ct);
        await _audit.LogAsync("OTP_ACTION_REQUESTED", $"Action OTP requested for scope {command.ScopeId}", user.UserName, ct: ct);
    }

    public async Task<PendingSendCommand?> ConfirmAndConsumeCommandAsync(ApplicationUser user, string scopeId, string otpCode, CancellationToken ct = default)
    {
        var key = GetCommandKey(user.Id, scopeId);
        if (!_cache.TryGetValue(key, out PendingSendCommand? command) || command is null)
        {
            return null;
        }

        var isValid = await _userManager.VerifyUserTokenAsync(user, "SendActionOtp", GetPurpose(scopeId), otpCode);
        if (!isValid)
        {
            await _audit.LogAsync("OTP_ACTION_FAILED", $"Invalid action OTP for scope {scopeId}", user.UserName, ct: ct);
            return null;
        }

        _cache.Remove(key);
        await _audit.LogAsync("OTP_ACTION_CONFIRMED", $"Action OTP confirmed for scope {scopeId}", user.UserName, ct: ct);
        return command;
    }

    private static string GetCommandKey(string userId, string scopeId) => $"poc-send-command:{userId}:{scopeId}";
    private static string GetPurpose(string scopeId) => $"send-confirm:{scopeId}";
}
