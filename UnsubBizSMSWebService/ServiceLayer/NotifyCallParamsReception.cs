using System;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using UnsubBizSMSWebService.Exceptions;
using UnsubBizSMSWebService.SendSMShttp2sms;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public class NotifyCallParamsReception
    {
        http2sms h2s;
        private string msisdn;
        private int clientCode;
        
        public NotifyCallParamsReception(string msisdn, int clientCode)
        {
            h2s = new http2sms();
            this.msisdn = msisdn;
            this.clientCode = clientCode;
        }

        private static string ConStr = ConfigurationManager.ConnectionStrings["BizSMS"].ConnectionString;

        public int UnsubscribeUserNotInMTSProcessing()
        {
            SmsMessage smsMessage = new SmsMessage()
            {
                message = "",
                senderAddress = "tel:" + msisdn,
                smsServiceActivationNumber = ""
            };
            var sms = new SmsMessageSender();

            var userNumber = "0" + msisdn.Substring(msisdn.Trim().IndexOf("381") + "381".Length);

            var msgId = "-1";

            try
            {
                //Log incoming parameters
                var clientCodeString = clientCode.ToString();
                Logger.LogIncomingParams(msisdn, clientCodeString);
                //Unsubscribe user
                UnsubUserNumberNotInMts(userNumber, clientCode);
                //Log successful unsubscription
                Logger.LogSuccessUnsub(msisdn, clientCodeString);
            }
            catch (BadNumberOrCLientIDException)
            {
                msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["BadNumberOrCLientIDException"].ToString(), h2s, msisdn);

                Logger.LogMessage("NotifyCallParamsReception: Korisniku " + userNumber + " (koji je ukucao kod: " + clientCode + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["BadNumberOrCLientIDException"] + ", msgId: " + msgId);

                return 0;
            }
            catch (UserAlreadyUnsubException)
            {
                msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["UserAlreadyUnsubException"].ToString(), h2s, msisdn);

                Logger.LogMessage("NotifyCallParamsReception: Korisniku " + userNumber + " (koji je ukucao kod: " + clientCode + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["UserAlreadyUnsubException"] + ", msgId: " + msgId);

                return 0;
            }
            catch (UserInVpnGroupException)
            {
                msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["UserInVpnGroupException"].ToString(), h2s, msisdn);

                Logger.LogMessage("NotifyCallParamsReception: Korisniku " + userNumber + " (koji je ukucao kod: " + clientCode + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["UserInVpnGroupException"] + ", msgId: " + msgId);

                return 0;
            }
            catch (DatabaseProcessException ex)
            {
                msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"].ToString(), h2s, msisdn);

                Logger.LogMessage("NotifyCallParamsReception: Korisniku " + userNumber + " (koji je ukucao kod: " + clientCode + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"] + ", msgId: " + msgId);

                SendErrorMail.SendMailSystem("Error unsubscribing user from BizSMS", "Fail: UnsubUserNumberNotInMts(userNumber: " + userNumber + ", clientCode: " + clientCode + "), ErrorMesssage: " + ex);

                return 0;
            }
            catch (SqlException ex)
            {
                using (var conn = new SqlConnection(ConStr))
                using (var command = new SqlCommand("Log_Insert", conn))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    DateTime logDate = DateTime.Now;
                    string logLevel = "ERROR";
                    string logSource = "UnsubBizSMSWebService.ServiceLayer.NotifyCall";
                    string logUser = "0800_Odjava";
                    string logAction = "UnsubscribeUserNotInMTSProcessing()";
                    var logEx = ex;

                    command.Parameters.Add("@Log_Date", SqlDbType.DateTime).Value = logDate;
                    command.Parameters.Add("@Log_Level", SqlDbType.VarChar).Value = logLevel;
                    command.Parameters.Add("@Log_Source", SqlDbType.VarChar).Value = logSource;
                    command.Parameters.Add("@User", SqlDbType.VarChar).Value = logUser;
                    command.Parameters.Add("@Controller", SqlDbType.VarChar).Value = "UnsubBizSMSService.svc";
                    command.Parameters.Add("@Action", SqlDbType.VarChar).Value = logAction;
                    command.Parameters.Add("@Log_Message", SqlDbType.VarChar).Value = "Sql greska pri odjavi korisnika pozivanjem 0800 broja.";
                    command.Parameters.Add("@Exception", SqlDbType.VarChar).Value = logEx.ToString();

                    conn.Open();
                    command.ExecuteNonQuery();
                };

                msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"].ToString(), h2s, msisdn);

                Logger.LogMessage("NotifyCallParamsReception: Korisniku " + userNumber + " (koji je ukucao kod: " + clientCode + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["DatabaseProcessOrSqlException"] + ", msgId: " + msgId);

                SendErrorMail.SendMailSystem("Error unsubscribing user from BizSMS", "Fail: UnsubUserNumberNotInMts(userNumber: " + userNumber + ", clientCode: " + clientCode + "), ErrorMesssage: " + ex);
                
                return 0;
            }
            catch(Exception ex)
            {
                msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["GeneralException"].ToString(), h2s, msisdn);

                Logger.LogMessage("NotifyCallParamsReception: Korisniku " + userNumber + " (koji je ukucao kod: " + clientCode + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["GeneralException"] + ", msgId: " + msgId);

                SendErrorMail.SendMailSystem("Error unsubscribing user from BizSMS", "Fail: UnsubUserNumberNotInMts(userNumber: " + userNumber + ", clientCode: " + clientCode + "), ErrorMesssage: " + ex);

                return 0;
            }
            
            //send sms with message that Unsubscription is successful
            msgId = sms.SendSmsToNonMts(ConfigurationManager.AppSettings["Sender"].ToString(), ConfigurationManager.AppSettings["SuccessUnsubscription"].ToString(), h2s, msisdn);

            Logger.LogMessage("NotifyCallParamsReception: Korisniku " + userNumber + " (koji je ukucao kod: " + clientCode + ") se salje povratni SMS: " + ConfigurationManager.AppSettings["SuccessUnsubscription"] + ", msgId: " + msgId);

            return 1;
        }

        public void UnsubUserNumberNotInMts(string userNumber, int clientCode)
        {
            using (var conn = new SqlConnection(ConStr))
            using (var command = new SqlCommand("sp_unsubscribeUserNotInMTS", conn))
            {
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@ClientCode", SqlDbType.VarChar).Value = clientCode;
                command.Parameters.Add("@UserNumber", SqlDbType.VarChar).Value = userNumber;

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

                    case (int)ReturnDbEnums.Number_or_CLientID_NonExisting:
                        throw new BadNumberOrCLientIDException();

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

    }
}