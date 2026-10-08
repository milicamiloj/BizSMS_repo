namespace BizSMS.Web.Services
{
    // Apstrakcija nad SelfCareLdapWS proverom tipa broja (U MTS / VAN MTS).
    public interface ILdapChecker
    {
        // broj06: broj u formatu 06XXXXXXXX. Vraća "U MTS" ako postoji u MTS LDAP-u, inače "VAN MTS".
        Task<string> GetNumberTypeAsync(string broj06);
    }
}
