using Microsoft.Web.Services3.Design;
using System;
using System.Web.Services.Protocols;
using UnsubBizSMSWebService.SendSMSReference;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public class SendSmsProcesor : ISendSmsProcesor
    {
        private string _username;
        private string _password;
        private string[] _msisdn;
        private string _senderName;
        private string _msgText;
        private SimpleReference _receiptRequest;
        private SendSMSReference.ChargingInformation _charging;
        public string Username
        {

            get
            {
                return _username;
            }

            set
            {
                var val = value;
                if (string.IsNullOrEmpty(val)) throw new Exception("Unesi korisničko ime");
                if (val.IndexOf('@') < 0) throw new Exception("Neispravno korisnicko ime, nedostaje \"@\"");
                _username = value;
            }

        }
        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                var val = value;
                if (string.IsNullOrEmpty(val)) throw new Exception("Password ne sme biti prazno polje");
                if (value.Length < 3) throw new Exception("Neispravan password, mora imati više od dva karaktera");
                _password = value;
            }
        }
        public string[] Msisdn
        {
            get
            {
                return _msisdn;
            }
            set
            {
                if (value.Length == 0 || string.IsNullOrEmpty(value[0])) throw new Exception("msisdn nije definisan");
                if (!(value[0].Contains("tel:") || value[0].Contains("session:"))) throw new Exception("polje address mora sadržati tel: ili session:");
                _msisdn = value;
            }
        }

        public string SenderName
        {
            get
            {
                return _senderName;
            }
            set
            {
                var val = value;
                if (string.IsNullOrEmpty(val)) throw new Exception("SenderName ne sme biti prazno polje");
                _senderName = value;
            }
        }

        public string MsgText
        {
            get
            {
                return _msgText;
            }
            set
            {
                _msgText = value;
            }
        }

        public SimpleReference ReceiptRequest
        {
            get
            {
                return _receiptRequest;
            }
            set
            {
                _receiptRequest = value;
            }
        }

        public SendSMSReference.ChargingInformation Charging
        {
            get
            {
                return _charging;
            }
            set
            {
                _charging = value;
            }
        }


        public string SendSMS()
        {
            SendSmsService ssmss = AssertCustomHeader();
            string result;
            try
            {
                result = ssmss.sendSms(_msisdn, _senderName, _charging, _msgText, _receiptRequest);
            }
            catch (SoapException ex)
            {
                Logger.LogMessage($"{ex.Detail.InnerText}; {ex.Message}");
                throw new Exception($"{ex.Detail.InnerText}; {ex.Message}");
            }
            catch (Exception ex)
            {
                Logger.LogMessage(ex.Message);
                throw new Exception(ex.Message);
            }

            return result;
        }

        public SendSmsService AssertCustomHeader()
        {
            var ssmss = new SendSmsService();
            Policy policy = new Policy();
            CustomHeadersAssertion customHeader = new CustomHeadersAssertion();
            customHeader.Username = _username;
            customHeader.Password = _password;
            policy.Assertions.Add(customHeader);

            ssmss.SetPolicy(policy);
            return ssmss;
        }
    }
}