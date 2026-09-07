namespace BizSMS.Poc.Net10.Services;

public interface IOtpSender
{
    Task SendAsync(string destinationPhoneNumber, string code, string purpose, CancellationToken ct = default);
}
