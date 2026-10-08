using BizSMS.Web.Helpers;

namespace BizSMS.Web.Services
{
    // Privremena zamena za legacy SelfCareLdapWS SOAP klijent (WCF/WSE3 ne radi na Core-u).
    // TODO Faza 5 (§10.1): regenerisati klijent (dotnet-svcutil/CoreWCF) i pozvati vratiTipKorisnika
    // sa "Integration-Auth" headerom, pa vratiti "U MTS"/"VAN MTS" na osnovu odgovora.
    public class LdapCheckerStub : ILdapChecker
    {
        private readonly Logger _logger = new Logger();

        public Task<string> GetNumberTypeAsync(string broj06)
        {
            _logger.Warn($"LDAP stub: broj {broj06} tretiran kao 'VAN MTS' (prava provera dolazi u Fazi 5)");
            return Task.FromResult("VAN MTS");
        }
    }
}
