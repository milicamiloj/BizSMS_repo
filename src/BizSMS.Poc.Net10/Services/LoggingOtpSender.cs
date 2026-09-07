using System.Text.RegularExpressions;

namespace BizSMS.Poc.Net10.Services;

public sealed class LoggingOtpSender : IOtpSender
{
    private readonly ILogger<LoggingOtpSender> _logger;

    public LoggingOtpSender(ILogger<LoggingOtpSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string destinationPhoneNumber, string code, string purpose, CancellationToken ct = default)
    {
        var masked = Regex.Replace(destinationPhoneNumber, @"\d(?=\d{2})", "*");
        _logger.LogInformation("POC OTP sent. Purpose={Purpose}, Phone={Phone}", purpose, masked);
        return Task.CompletedTask;
    }
}
