namespace BizSMS.Web.Services
{
    // Apstrakcija slanja SMS-a (2FA kod, kampanje). Zamena za legacy UserManager.SmsService (IIdentityMessageService).
    public interface ISmsSender
    {
        // Vraca identifikator poruke koji SDP vrati (element <result>).
        Task<string> SendAsync(string phoneNumber, string message);
    }
}
