namespace BizSMS.Web.Services
{
    // Apstrakcija slanja SMS-a (2FA kod, kampanje). Zamena za legacy UserManager.SmsService (IIdentityMessageService).
    public interface ISmsSender
    {
        // Vraca identifikator poruke koji SDP vrati (element <result>).
        // Salje sa podrazumevanim posiljaocem (Sdp:Alphanumeric, npr. MTS) — koristi 2FA.
        Task<string> SendAsync(string phoneNumber, string message);

        // Kampanjsko slanje: eksplicitni posiljalac (alfanumerik klijenta) umesto podrazumevanog.
        Task<string> SendAsync(string phoneNumber, string message, string senderName, string endpoint, string correlator);
    }
}
