using System.ServiceModel;
using System.Xml;
using UnsubBizSMSWebService.ServiceLayer;

namespace UnsubBizSMSWebService
{
    [ServiceBehavior(Namespace = "http://www.csapi.org/schema/parlayx/sms/notification/v2_1/local")]
    public class UnsubBizSMSService : IUnsubBizSMSService

    {
        //metoda koja prihvata parametre za odjavu usera preko SMS-a i vraca prazan string u svakom slucaju
        public void notifySmsReception(string correlator, XmlNode[] Message)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(o =>
            {
                var notify = new NotifySmsReception(correlator, Message);
                notify.UnsubscribeUserInMTSProcessing();
            });
            //return string.Empty;
        }

        //metoda koja prihvata parametre za odjavu usera preko 0800 broja i vraca int 1 ako je uspesna ili int 0 ako je neuspesna odjava
        public int notifyCallParamsReception(string msisdn, int clientCode)
        {
            var notify = new NotifyCallParamsReception(msisdn, clientCode);

            int result = notify.UnsubscribeUserNotInMTSProcessing();

            return result;
        }

        
    }
}
