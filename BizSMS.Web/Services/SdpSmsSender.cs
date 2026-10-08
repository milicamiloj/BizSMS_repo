using System.Security;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace BizSMS.Web.Services
{
    // Konfiguracija SDP-a (sekcija "Sdp" iz appsettings.json / User Secrets).
    public class SdpOptions
    {
        public string Url { get; set; } = "";
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string Alphanumeric { get; set; } = "MTS";
    }

    // Salje SMS preko SDP ParlayX SMS v2.1 (operacija sendSms) sa WS-Security UsernameToken (PasswordText) headerom.
    // Zamena za legacy SDPSendSms klijent (WSE3 Policy/CustomHeadersAssertion) koji ne radi na .NET-u:
    // SOAP envelope se pravi rucno i salje preko HttpClient-a. Struktura verno prati stari generisani proxy.
    public class SdpSmsSender : ISmsSender
    {
        private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
        private const string LocalNs = "http://www.csapi.org/schema/parlayx/sms/send/v2_1/local";
        private const string WsseNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
        private const string WsuNs = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
        private const string PasswordTextType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText";

        private readonly HttpClient _http;
        private readonly SdpOptions _opt;
        private readonly ILogger<SdpSmsSender> _logger;

        public SdpSmsSender(HttpClient http, IOptions<SdpOptions> opt, ILogger<SdpSmsSender> logger)
        {
            _http = http;
            _opt = opt.Value;
            _logger = logger;
        }

        public async Task<string> SendAsync(string phoneNumber, string message)
        {
            if (string.IsNullOrWhiteSpace(_opt.Url) || _opt.Url.Contains("__") ||
                string.IsNullOrWhiteSpace(_opt.Username) ||
                string.IsNullOrWhiteSpace(_opt.Password) || _opt.Password.Contains("__"))
            {
                throw new InvalidOperationException(
                    "SDP nije konfigurisan (Sdp:Url / Sdp:Username / Sdp:Password). Postavi vrednosti kroz User Secrets.");
            }

            // Legacy format: "tel:381" + broj bez vodece nule (npr. 0641234567 -> tel:381641234567)
            var address = "tel:381" + phoneNumber.TrimStart().Substring(1);
            var envelope = BuildEnvelope(address, message);

            using var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"\""); // ParlayX sendSms koristi prazan SOAPAction

            using var response = await _http.PostAsync(_opt.Url, content);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("SDP sendSms HTTP {Status}: {Body}", (int)response.StatusCode, body);
                throw new HttpRequestException($"SDP sendSms nije uspeo (HTTP {(int)response.StatusCode}).");
            }

            var result = ParseResult(body);
            _logger.LogInformation("SDP sendSms result: {Result}", result);
            return result;
        }

        private string BuildEnvelope(string address, string message)
        {
            var user = SecurityElement.Escape(_opt.Username);
            var pass = SecurityElement.Escape(_opt.Password);
            var addr = SecurityElement.Escape(address);
            var sender = SecurityElement.Escape(_opt.Alphanumeric);
            var msg = SecurityElement.Escape(message);

            return
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""{SoapNs}"">
  <soap:Header>
    <wsse:Security soap:mustUnderstand=""1"" xmlns:wsse=""{WsseNs}"">
      <wsse:UsernameToken xmlns:wsu=""{WsuNs}"">
        <wsse:Username>{user}</wsse:Username>
        <wsse:Password Type=""{PasswordTextType}"">{pass}</wsse:Password>
      </wsse:UsernameToken>
    </wsse:Security>
  </soap:Header>
  <soap:Body>
    <sendSms xmlns=""{LocalNs}"">
      <addresses>{addr}</addresses>
      <senderName>{sender}</senderName>
      <charging />
      <message>{msg}</message>
    </sendSms>
  </soap:Body>
</soap:Envelope>";
        }

        private static string ParseResult(string soapResponse)
        {
            try
            {
                var doc = XDocument.Parse(soapResponse);
                var result = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "result");
                return result?.Value ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
