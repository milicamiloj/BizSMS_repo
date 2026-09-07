namespace BizSMS.Poc.Net10.Services;

public interface IPocDbVerificationService
{
    Task<object> VerifyReadWriteAsync(CancellationToken ct = default);
}
