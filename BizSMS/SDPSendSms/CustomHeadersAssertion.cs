using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Web.Services3.Design;
using Microsoft.Web.Services3;
using System.Xml;
using System.Configuration;

namespace BizSMS.SDPSendSms
{

    public class CustomHeadersAssertion : PolicyAssertion
    {
        public override SoapFilter CreateClientInputFilter(FilterCreationContext context)
        {
            return new ClientInputFilter();
        }

        public override SoapFilter CreateClientOutputFilter(FilterCreationContext context)
        {
            return new ClientOutputFilter();
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
        public ClientOutputFilter()
            : base()
        { }
        public override SoapFilterResult ProcessMessage(SoapEnvelope envelope)
        {
            

            XmlNode securityNode = envelope.CreateNode(XmlNodeType.Element, "wsse:Security", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            XmlAttribute securityAttr = envelope.CreateAttribute("soap:mustUnderstand");
            securityAttr.Value = "1";
            XmlNode usernameTokenNode = envelope.CreateNode(XmlNodeType.Element, "wsse:UsernameToken", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            XmlElement userElement = usernameTokenNode as XmlElement;
            userElement.SetAttribute("xmlns:wsu", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd");
            XmlNode userNameNode = envelope.CreateNode(XmlNodeType.Element, "wsse:Username", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            userNameNode.InnerXml = ConfigurationManager.AppSettings["SdpUsername"];       
            XmlNode pwdNode = envelope.CreateNode(XmlNodeType.Element, "wsse:Password", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd");
            XmlElement pwdElement = pwdNode as XmlElement;
            pwdElement.SetAttribute("Type", "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordText");
            pwdNode.InnerXml = ConfigurationManager.AppSettings["SdpPassword"];     
            usernameTokenNode.AppendChild(userNameNode);
            usernameTokenNode.AppendChild(pwdNode);
            securityNode.AppendChild(usernameTokenNode);
            envelope.ImportNode(securityNode, true);
            XmlNode node = envelope.Header;
            node.AppendChild(securityNode);
            return SoapFilterResult.Continue;
        }
    }


}