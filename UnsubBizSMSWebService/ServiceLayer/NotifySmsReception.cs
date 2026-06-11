using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Xml;
using UnsubBizSMSWebService.Exceptions;
using UnsubBizSMSWebService.SendSMShttp2sms;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public class NotifySmsReception
    {
        private static string ConStr = ConfigurationManager.ConnectionStrings["BizSMS"].ConnectionString;
        http2sms h2s;
        private string correlator;
        private XmlNode[] message;

        public NotifySmsReception(string Correlator, XmlNode[] Message)
        {
            h2s = new http2sms();
            correlator = Correlator;
            message = Message;
        }

        public void UnsubscribeUserInMTSProcessing()
        {
            var path = AppDomain.CurrentDomain.BaseDirectory;
            SmsMessage smsMessage = new SmsMessage()
            {
                //for publish
                message = message[0].InnerText,
                senderAddress = message[1].InnerText,
                smsServiceActivationNumber = message[2].InnerText

                //for test
                //message = message[1].InnerText.Replace("\n", "").Trim(),
                //senderAddress = (message[3].InnerText.Replace("\n", "").Trim()).Split('#')[1],
                //smsServiceActivationNumber = message[5].InnerText.Replace("\n", "").Trim()
            };

            var sms = new SmsMessageSender();
            //smsMessage.senderAddress = smsMessage.senderAddress;
            //var msgId = "-1";

            //moze se koristiti i StringComparison.CurrentCultureIgnoreCase
            if (smsMessage.message.IndexOf("STOP", StringComparison.OrdinalIgnoreCase) == -1)
            {
                Logger.LogMessage("NotifySMSReception: Korisniku " + smsMessage.senderAddress + " se salje povratni SMS: " + ConfigurationManager.AppSettings["KeyWordSTOPMissing"]);

                sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["KeyWordSTOPMissing"]);

                //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["KeyWordSTOPMissing"].ToString(), h2s, smsMessage.senderAddress);
                return;
            }

            var userNumber = "0" + smsMessage.senderAddress.Substring(smsMessage.senderAddress.Trim().IndexOf("381") + "381".Length);
            var clientID = smsMessage.message.Substring(smsMessage.message.Trim().IndexOf("STOP ", StringComparison.OrdinalIgnoreCase) + 5);

            //var isclientIdNumber = clientID.All(Char.IsDigit);

            if (!IsDigitsOnly(clientID))
            {
                //Log incoming parameters
                Logger.LogIncomingParams(userNumber, clientID);
                Logger.LogMessage("NotifySMSReception: Korisniku " + userNumber + " (clientID: " + clientID + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["BadCLientIDException"]);

                //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["BadCLientIDException"].ToString(), h2s, smsMessage.senderAddress);
                sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["BadCLientIDException"]);
                return;
            }

            if (clientID.Length > 5 || clientID.Length < 1)
            {
                //Log incoming parameters
                Logger.LogIncomingParams(userNumber, clientID);
                Logger.LogMessage("NotifySMSReception: Korisniku " + userNumber + " (clientID: " + clientID + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["BadAlphanumException"]);

                //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["BadAlphanumException"].ToString(), h2s, smsMessage.senderAddress);
                sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["BadAlphanumException"]);
                return;
            }

            try
            {
                //Log incoming parameters
                Logger.LogIncomingParams(userNumber, clientID);
                //Unsubscribe user
                UnsubscribeUserNumberInMts(clientID, userNumber);
                //Log successful unsubscription
                Logger.LogSuccessUnsub(userNumber, clientID);
            }
            catch (BadAlphanumException)
            {
                Logger.LogMessage("NotifySMSReception: Korisniku " + userNumber + " (clientID: " + clientID + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["BadAlphanumException"]);

                //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["BadAlphanumException"].ToString(), h2s, smsMessage.senderAddress);

                sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["BadAlphanumException"].ToString());
                return;
            }
            catch (UserAlreadyUnsubException)
            {
                Logger.LogMessage("NotifySMSReception: Korisniku " + userNumber + " (clientID: " + clientID + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["UserAlreadyUnsubException"]);

                //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["UserAlreadyUnsubException"].ToString(), h2s, smsMessage.senderAddress);

                sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["UserAlreadyUnsubException"].ToString());
                return;

            }
            catch (UserInVpnGroupException)
            {
                Logger.LogMessage("NotifySMSReception: Korisniku " + userNumber + " (clientID: " + clientID + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["UserInVpnGroupException"]);

                //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["UserInVpnGroupException"].ToString(), h2s, smsMessage.senderAddress);

                sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["UserInVpnGroupException"].ToString());
                return;
            }
            catch (DatabaseProcessException ex)
            {
                Logger.LogMessage("NotifySMSReception: Korisniku " + userNumber + " (clientID: " + clientID + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"]);

                //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"].ToString(), h2s, smsMessage.senderAddress);

                sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"].ToString());

                SendErrorMail.SendMailSystem("Error unsubscribing user from BizSMS", "Fail: UnsubscribeUserNumberInMts(clientID: " + clientID + ", userNumber: " + userNumber + "), ErrorMesssage: " + ex);

                return;
            }
            catch (SqlException ex)
            {
                using (var conn = new SqlConnection(ConStr))
                using (var command = new SqlCommand("Log_Insert", conn))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    DateTime logDate = DateTime.Now;
                    string logLevel = "ERROR";
                    string logSource = "UnsubBizSMSWebService.ServiceLayer.NotifySms";
                    string logUser = "SmsOdjava";
                    string logAction = "UnsubscribeUserProcessing";
                    var logEx = ex;

                    command.Parameters.Add("@Log_Date", SqlDbType.DateTime).Value = logDate;
                    command.Parameters.Add("@Log_Level", SqlDbType.VarChar).Value = logLevel;
                    command.Parameters.Add("@Log_Source", SqlDbType.VarChar).Value = logSource;
                    command.Parameters.Add("@User", SqlDbType.VarChar).Value = logUser;
                    command.Parameters.Add("@Controller", SqlDbType.VarChar).Value = "UnsubBizSMSService.svc";
                    command.Parameters.Add("@Action", SqlDbType.VarChar).Value = logAction;
                    command.Parameters.Add("@Log_Message", SqlDbType.VarChar).Value = "Sql greska pri SMS odjavi korisnika";
                    command.Parameters.Add("@Exception", SqlDbType.VarChar).Value = logEx.ToString();

                    conn.Open();
                    command.ExecuteNonQuery();
                };
                Logger.LogMessage("NotifySMSReception: Korisniku " + userNumber + " (clientID: " + clientID + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"]);

                //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"].ToString(), h2s, smsMessage.senderAddress);

                sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"].ToString());

                SendErrorMail.SendMailSystem("Error unsubscribing user InMts from BizSMS", "Fail: UnsubscribeUserNumberInMts(clientID: " + clientID + ", userNumber: " + userNumber + "), ErrorMesssage: " + ex);

                return;
            }
            catch (Exception ex)
            {
                Logger.LogMessage("NotifySMSReception: Korisniku " + userNumber + " (clientID: " + clientID + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["GeneralException"]);

                //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["GeneralException"].ToString(), h2s, smsMessage.senderAddress);

                sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["GeneralException"].ToString());

                SendErrorMail.SendMailSystem("Error unsubscribing user InMts from BizSMS", "Fail: UnsubscribeUserNumberInMts(clientID: " + clientID + ", userNumber: " + userNumber + "), ErrorMesssage: " + ex);

                return;
            }
            Logger.LogMessage("NotifySMSReception: Korisniku " + userNumber + " (clientID: " + clientID + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["SuccessUnsubscription"]);

            //send sms with message that everything is ok
            //msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["SuccessUnsubscription"].ToString(), h2s, smsMessage.senderAddress);

            sms.SendMessage(smsMessage.senderAddress, ConfigurationManager.AppSettings["SuccessUnsubscription"].ToString());
        }

        //calls db stored procedure to change the status of Send_allowed to 0
        private void UnsubscribeUserNumberInMts(string clientID, string userNumber)
        {
            var clientIDInt = int.Parse(clientID);

            using (var conn = new SqlConnection(ConStr))
            using (var command = new SqlCommand("sp_unsubscribeUser", conn))
            {
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@UserNumber", SqlDbType.VarChar).Value = userNumber;
                command.Parameters.Add("@ClientID", SqlDbType.VarChar).Value = clientIDInt;

                //set return parameter to check status from db
                SqlParameter returnParameter = new SqlParameter("@Result", SqlDbType.VarChar, 50);
                returnParameter.Direction = ParameterDirection.ReturnValue;
                command.Parameters.Add(returnParameter);

                try
                {
                    conn.Open();
                    command.ExecuteNonQuery();
                }
                catch (SqlException)
                {
                    throw;
                }

                var returnValue = (int)returnParameter.Value;
                
                switch (returnValue)
                {
                    case (int)ReturnDbEnums.DatabaseProcessFailure:
                        throw new DatabaseProcessException();

                    case (int)ReturnDbEnums.Number_or_AlphanumNonExisting:
                        throw new BadAlphanumException();

                    case (int)ReturnDbEnums.UserInVpnGroup:
                        throw new UserInVpnGroupException();

                    case (int)ReturnDbEnums.SuccessUnsub:
                        break;

                    case (int)ReturnDbEnums.UserAlreadyUnsub:
                        throw new UserAlreadyUnsubException();
                    default:
                        throw new Exception();
                }
            };
        }
        bool IsDigitsOnly(string str)
        {
            foreach (char c in str)
            {
                if (c < '0' || c > '9')
                    return false;
            }

            return true;
        }
    }
}