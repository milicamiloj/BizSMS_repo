using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.DirectoryServices;

namespace BizSMS.Helpers
{
    public class LDAP
    {
        private DirectorySearcher Searcher;

        public LDAP()
        {
            Initialize();
        }

        private void Initialize()
        {
            string LDAPUser = System.Configuration.ConfigurationManager.AppSettings["LDAPUser"];
            DirectoryEntry Entry = new DirectoryEntry();
            Entry.AuthenticationType = AuthenticationTypes.ServerBind;
            Entry.Path = System.Configuration.ConfigurationManager.AppSettings["LDAPAddress"];
            Entry.Username = LDAPUser;
            Entry.Password = System.Configuration.ConfigurationManager.AppSettings["LDAPPass"];

            Searcher = new DirectorySearcher(Entry);
        }

        public bool isExist(string MSISDN)
        {
            Searcher.Filter = ("(&(objectClass=*)(MSISDN=" + MSISDN + "))");

            return Searcher.FindAll() != null ? true : false;
        }

        ~LDAP()
        {
            Searcher.Dispose();
        }
    }
}