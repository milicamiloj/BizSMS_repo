using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Xml;

namespace UnsubBizSMSWebService
{
    [ServiceContract(Namespace = "http://www.csapi.org/schema/parlayx/sms/notification/v2_1/local")]

    public interface IUnsubBizSMSService
    {
        [OperationContract(Action = "")] //for publish uncomment Action
        void notifySmsReception(string correlator, XmlNode[] message);

        [OperationContract(Action = "notifyCallParamsReception")]
        int notifyCallParamsReception(string msisdn, int clientCode);
    }


    [DataContract(Namespace = "http://www.csapi.org/schema/parlayx/sms/notification/v2_1/local")]
    public class SmsMessage
    {
        [DataMember(Name = "message")]
        public string message { get; set; }

        [DataMember]
        public string senderAddress { get; set; }

        [DataMember]
        public string smsServiceActivationNumber { get; set; }
    }
}
