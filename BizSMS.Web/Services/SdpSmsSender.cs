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

    public class ReceiptReq
    {
        public string Correlator { get; set; } = "16";
        public string Endpoint { get; set; } = "http://http2smsraz.telekom.rs/http2sms.asmx";
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
        private readonly ReceiptReq _receiptReq;

        public SdpSmsSender(HttpClient http, IOptions<SdpOptions> opt, ILogger<SdpSmsSender> logger, ReceiptReq receipt)
        {
            _http = http;
            _opt = opt.Value;
            _logger = logger;
            _receiptReq = receipt;
        }

        // 2FA: podrazumevani posiljalac (Sdp:Alphanumeric, npr. MTS).
        public Task<string> SendAsync(string phoneNumber, string message)
            => SendAsync(phoneNumber, message, _opt.Alphanumeric, _receiptReq.Endpoint, _receiptReq.Correlator);

        public async Task<string> SendAsync(string phoneNumber, string message, string senderName, string endpoint, string correlator) //, receiptReq receipt)
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
            var envelope = BuildEnvelope(address, message, senderName, endpoint, correlator);

            using var content = new StringContent(envelope, Encoding.UTF8, "text/xml");
            content.Headers.Add("SOAPAction", "\"\""); // ParlayX sendSms koristi prazan SOAPAction

            using var response = await _http.PostAsync(_opt.Url, content);
            var body = await response.Content.ReadAsStringAsync();

            // Ceo odgovor SDP-a (bez tajni) — ključno za debug slanja (npr. 2FA koda).
            _logger.LogInformation("SDP sendSms HTTP {Status}, odgovor: {Body}", (int)response.StatusCode, body);

            // SDP vraca SOAP Fault i sa HTTP 200 — mora da se detektuje, inace se greska progutа
            // i tok misli da je poruka poslata (kod "poslat" ali SMS nikad ne stigne).
            var fault = ParseFault(body);
            if (fault != null)
            {
                _logger.LogError("SDP sendSms SOAP Fault: {Fault}", fault);
                throw new InvalidOperationException($"SDP sendSms vratio grešku: {fault}");
            }

            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"SDP sendSms nije uspeo (HTTP {(int)response.StatusCode}).");

            var result = ParseResult(body);
            if (string.IsNullOrEmpty(result))
                _logger.LogWarning("SDP sendSms: nema <result> elementa u odgovoru (moguć problem sa envelope-om).");

            _logger.LogInformation("SDP sendSms result: {Result}", result);
            return result;
        }

        private string BuildEnvelope(string address, string message, string senderName, string endpoint, string correlator)
        {
            var user = SecurityElement.Escape(_opt.Username);
            var pass = SecurityElement.Escape(_opt.Password);
            var addr = SecurityElement.Escape(address);
            var sender = SecurityElement.Escape(senderName);
            var msg = SecurityElement.Escape(message);
            // ParlayX receiptRequest (SimpleReference, tip iz common/v2_1): deca su Unqualified
            // (prazan namespace -> xmlns=""). Kad je endpoint prazan, blok se izostavlja — legacy
            // sinhroni sendSms za 2FA kod uopšte ne šalje receiptRequest (addresses/senderName/charging/message).
            var receipt = string.IsNullOrWhiteSpace(endpoint)
                ? ""
                : $@"
      <receiptRequest>
        <endpoint xmlns="""">{SecurityElement.Escape(endpoint)}</endpoint>
        <correlator xmlns="""">{SecurityElement.Escape(correlator)}</correlator>
      </receiptRequest>";

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
      <message>{msg}</message>{receipt}
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

        // Vraca tekst SOAP Fault-a (faultcode: faultstring) ako postoji, inace null.
        private static string? ParseFault(string soapResponse)
        {
            try
            {
                var doc = XDocument.Parse(soapResponse);
                var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
                if (fault == null)
                    return null;

                var code = fault.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultcode" or "Value")?.Value;
                var reason = fault.Descendants().FirstOrDefault(e => e.Name.LocalName is "faultstring" or "Text")?.Value;
                return string.Join(": ", new[] { code, reason }.Where(s => !string.IsNullOrWhiteSpace(s)));
            }
            catch
            {
                return null;
            }
        }
    }
}
