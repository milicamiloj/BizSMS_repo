using Microsoft.Web.Services3;
using Microsoft.Web.Services3.Design;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Xml;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public class CustomHeadersAssertion : PolicyAssertion
    {
        public string Username { get; set; }

        public string Password { get; set; }
        public override SoapFilter CreateClientInputFilter(FilterCreationContext context)
        {
            return new ClientInputFilter();
        }

        public override SoapFilter CreateClientOutputFilter(FilterCreationContext context)
        {
            return new ClientOutputFilter(Username, Password);
        }

        public override SoapFilter CreateServiceInputFilter(FilterCreationContext context)
        {
            return new ServiceInputFilter();
        }

        public override SoapFilter CreateServiceOutputFilter(FilterCreationContext context)
        {
            return new ServiceOutputFilter();
        }

        public override System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string, Type>> GetExtensions()
        {
            return new KeyValuePair<string, Type>[] { new KeyValuePair<string, Type>("RemoveAddressingHeadersAssertion", this.GetType()) };
        }

        public override void ReadXml(XmlReader reader, IDictionary<string, Type> extensions) { reader.ReadStartElement("RemoveAddressingHeadersAssertion"); }

    }

    public class ClientInputFilter : SoapFilter
    {
        public override SoapFilterResult ProcessMessage(SoapEnvelope envelope)
        {
            return SoapFilterResult.Continue;
        }
    }

    public class ServiceInputFilter : SoapFilter
    {
        public override SoapFilterResult ProcessMessage(SoapEnvelope envelope)
        {
            return SoapFilterResult.Continue;
        }
    }

    public class ServiceOutputFilter : SoapFilter
    {
        public override SoapFilterResult ProcessMessage(SoapEnvelope envelope)
        {
            return SoapFilterResult.Continue;
        }
    }

    public class ClientOutputFilter : SoapFilter
    {
        public string Username { get; set; }

        public string Password { get; set; }

        public ClientOutputFilter()
            : base()
        { }

        public ClientOutputFilter(string username, string password) : base()
        {
            Username = username;
            Password = password;
        }

        public override SoapFilterResult ProcessMessage(SoapEnvelope envelope)
        {
            DataTable dtKratakBroj = new DataTable();
            string sdpUsername = Username;
            string sdpPassword = Password;// "telekom1.";


            XmlNode securityNode = envelope.CreateNode(XmlNodeType.Element, "wsse:Security", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            XmlAttribute securityAttr = envelope.CreateAttribute("soap:mustUnderstand");
            securityAttr.Value = "1";
            XmlNode usernameTokenNode = envelope.CreateNode(XmlNodeType.Element, "wsse:UsernameToken", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            XmlElement userElement = usernameTokenNode as XmlElement;
            userElement.SetAttribute("xmlns:wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            XmlNode userNameNode = envelope.CreateNode(XmlNodeType.Element, "wsse:Username", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            userNameNode.InnerXml = sdpUsername;
            XmlNode passwordNode = envelope.CreateNode(XmlNodeType.Element, "wsse:Password", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            XmlElement passwordElement = passwordNode as XmlElement;
            passwordElement.SetAttribute("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest");

            DateTime created = DateTime.Now;
            string createdStr = created.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            byte[] nonce = GenerateNonce(created);

            passwordNode.InnerXml = CreateDigestedPassword(nonce, createdStr);

            XmlNode createNode = envelope.CreateNode(XmlNodeType.Element, "wsse:Created", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            createNode.InnerXml = createdStr;

            XmlNode nonceNode = envelope.CreateNode(XmlNodeType.Element, "wsse:Nonce", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            nonceNode.InnerXml = Convert.ToBase64String(nonce);

            usernameTokenNode.AppendChild(userNameNode);
            usernameTokenNode.AppendChild(passwordNode);
            usernameTokenNode.AppendChild(createNode);
            usernameTokenNode.AppendChild(nonceNode);

            securityNode.AppendChild(usernameTokenNode);
            envelope.ImportNode(securityNode, true);
            XmlNode node = envelope.Header;
            node.AppendChild(securityNode);
            return SoapFilterResult.Continue;
        }
        private string CreateDigestedPassword(byte[] nonce, string createdStr)
        {
            IEnumerable<byte> byteNonce = nonce;
            IEnumerable<byte> byteCreated = Encoding.UTF8.GetBytes(createdStr);
            IEnumerable<byte> bytePassword = Encoding.UTF8.GetBytes(Password);
            IEnumerable<byte> concatenatedBytes = byteNonce.Concat(byteCreated).Concat(bytePassword);
            string digestedPassword = Convert.ToBase64String(SHA1Encrypt(concatenatedBytes.ToArray()));

            return digestedPassword;
        }

        private byte[] GenerateNonce(DateTime created)
        {
            Random r = new Random();
            UTF8Encoding encoder = new UTF8Encoding();
            string nonceStr = created + r.Next().ToString();
            var non = encoder.GetBytes(nonceStr);
            byte[] nonce = SHA1Encrypt(non);
            return nonce;
        }

        protected byte[] SHA1Encrypt(byte[] phrase)
        {
            SHA1CryptoServiceProvider sha1Hasher = new SHA1CryptoServiceProvider();
            byte[] hashedDataBytes = sha1Hasher.ComputeHash(phrase);
            return hashedDataBytes;
        }
    }
}