using System.Security;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Options;

namespace BizSMS.Web.Services
{
    // Konfiguracija SelfCareLdapWS (sekcija "Ldap" iz appsettings.json / User Secrets).
    public class LdapOptions
    {
        public string Url { get; set; } = "";
        // "Integration-Auth" HTTP header (base64 kredencijali) — tajna, ide u User Secrets.
        public string IntegrationAuth { get; set; } = "";
    }

    // Prava SelfCareLdapWS SOAP provera tipa broja (U MTS / VAN MTS) — zamena za LdapCheckerStub.
    // Legacy je koristio WCF LDAPCheckSoapClient (basicHttpBinding = SOAP 1.1) sa custom
    // "Integration-Auth" HTTP headerom; WCF/WSE3 ne radi na .NET-u pa SOAP envelope pravimo ručno
    // i šaljemo preko HttpClient-a (isti obrazac kao SdpSmsSender).
    //
    // Semantika iz legacy-ja (GroupController/AdminManageController): poziva se operacija
    // vratiTipKorisnika(msisdn); ako je rezultat NIJE null -> broj postoji u MTS LDAP-u -> "U MTS",
    // u suprotnom -> "VAN MTS".
    public class SelfCareLdapChecker : ILdapChecker
    {
        private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
        private const string TempuriNs = "http://tempuri.org/";
        private const string XsiNs = "http://www.w3.org/2001/XMLSchema-instance";
        private const string SoapAction = "http://tempuri.org/vratiTipKorisnika";

        private readonly HttpClient _http;
        private readonly LdapOptions _opt;
        private readonly ILogger<SelfCareLdapChecker> _logger;

        public SelfCareLdapChecker(HttpClient http, IOptions<LdapOptions> opt, ILogger<SelfCareLdapChecker> logger)
        {
            _http = http;
            _opt = opt.Value;
            _logger = logger;
        }

        public async Task<string> GetNumberTypeAsync(string broj06)
        {
            if (string.IsNullOrWhiteSpace(_opt.Url) || _opt.Url.Contains("__") ||
                string.IsNullOrWhiteSpace(_opt.IntegrationAuth) || _opt.IntegrationAuth.Contains("__"))
            {
                throw new InvalidOperationException(
                    "LDAP (SelfCareLdapWS) nije konfigurisan (Ldap:Url / Ldap:IntegrationAuth). Postavi vrednosti kroz User Secrets.");
            }

            // Legacy format: "381" + broj bez vodeće nule (npr. 0641234567 -> 381641234567)
            var msisdn = "381" + broj06.Trim().Substring(1);
            var envelope = BuildEnvelope(msisdn);

            using var request = new HttpRequestMessage(HttpMethod.Post, _opt.Url)
            {
                Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", "\"" + SoapAction + "\"");
            request.Headers.Add("Integration-Auth", _opt.IntegrationAuth);

            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("LDAP vratiTipKorisnika HTTP {Status}: {Body}", (int)response.StatusCode, body);
                throw new HttpRequestException($"LDAP vratiTipKorisnika nije uspeo (HTTP {(int)response.StatusCode}).");
            }

            var numberType = ParseNumberType(body);
            _logger.LogInformation("LDAP vratiTipKorisnika({Msisdn}) -> {NumberType}", msisdn, numberType);
            return numberType;
        }

        private static string BuildEnvelope(string msisdn)
        {
            var m = SecurityElement.Escape(msisdn);

            return
$@"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""{SoapNs}"">
  <soap:Body>
    <vratiTipKorisnika xmlns=""{TempuriNs}"">
      <msisdn>{m}</msisdn>
    </vratiTipKorisnika>
  </soap:Body>
</soap:Envelope>";
        }

        // Rezultat != null -> broj je u MTS LDAP-u -> "U MTS"; nil/odsutan element -> "VAN MTS".
        private static string ParseNumberType(string soapResponse)
        {
            try
            {
                var doc = XDocument.Parse(soapResponse);
                var result = doc.Descendants()
                    .FirstOrDefault(e => e.Name.LocalName == "vratiTipKorisnikaResult");

                if (result == null)
                    return "VAN MTS";

                var nil = result.Attribute(XName.Get("nil", XsiNs));
                if (nil != null && string.Equals(nil.Value, "true", StringComparison.OrdinalIgnoreCase))
                    return "VAN MTS";

                return "U MTS";
            }
            catch
            {
                // Nečitljiv/nevalidan odgovor tretiramo kao "nije u MTS-u" (konzervativno, kao legacy null).
                return "VAN MTS";
            }
        }
    }
}
