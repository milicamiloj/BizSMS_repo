using BizSMS.Attributes;
using BizSMS.Helpers;
using BizSMS.Models;
using Hangfire;
using Microsoft.AspNet.Identity;
using Oracle.ManagedDataAccess.Client;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Web;
using System.Web.Http;

namespace BizSMS.Controllers.API
{
    [AuthorizeApiUser(Roles = "Administrator")]
    [DefaultApiLogging]
    public class AdminManageController : ApiController
    {
        //protected ApplicationDbContext ApplicationDbContext { get; set; }

        private static string ConStr = ConfigurationManager.ConnectionStrings["BizSMS"].ConnectionString;

        protected UserManager<ApplicationUser> UserManager { get; set; }
        private ApplicationDbContext _db;

        Logger logger = new Logger();

        public AdminManageController()
        {
            db = new ApplicationDbContext();
        }

        public ApplicationDbContext db
        {
            get
            {
                return _db;
            }
            private set
            {
                _db = value;
            }
        }

        [HttpDelete]
        public IHttpActionResult Cancel(string id)
        {
            logger.SetControllerAction("AdminManageController:ApiController", "Cancel");
            if (string.IsNullOrEmpty(id))
            {
                logger.Warn("id is null or empty");
                return BadRequest();
            }
            logger.Info("Find user with id: " + id);
            var user = db.Users.Find(id);

            if (user == null)
            {
                logger.Warn("User not found");
                return NotFound();
            }

            user.IsCanceled = true;
            user.IsDeleted = true;
            
            db.SaveChanges();
            logger.Info("User deleted");
            return Ok();
        }

        [HttpDelete]
        public IHttpActionResult DeleteAlphanumeric(int id)
        {
            logger.SetControllerAction("AdminManageController:ApiController", "DeleteAlphanumeric");
            logger.Info("Find alphanumeric with id: " + id.ToString());
            AlphanumericModel model = db.Alphanumeric.Find(id);

            if (model == null)
            {
                logger.Warn("Alphanumeric not found");
                return NotFound();
            }

            db.Alphanumeric.Remove(model);

            db.SaveChanges();
            logger.Info("Alphanumeric deleted");
            return Ok();
        }

        [HttpGet]
        public IHttpActionResult ConfirmUploadNumbers()
        {
            logger.SetControllerAction("AdminManageController:ApiController", "ConfirmUploadNumbers");
            List<TempImportUpload> tempImport = null;
            try
            {
                logger.Info("Get temp data");
                tempImport = db.TempImport.Select(t => new TempImportUpload
                {
                    Name = t.Name,
                    Number = t.Number,
                    NumberType = t.NumberType.ToString()
                }).ToList();
            }
            catch
            {
                logger.Warn("Data not found");
                return NotFound();
            }
            TempImportData data = new TempImportData() { data = tempImport };
            return Ok(data);
        }

        [HttpGet]
        public IHttpActionResult GetNumbers(int? clientId)
        {
            if (clientId == null) return NotFound();

            logger.SetControllerAction("AdminManageController:ApiController", "GetNumbers");
            try
            {

                var numbers = (from gnumbers in db.Numbers
                               where gnumbers.ClientID == clientId && gnumbers.Active
                               select new
                               {
                                   NumberID = gnumbers.NumberID,
                                   NumberType = gnumbers.NumberType.Name,
                                   Number = gnumbers.Number,
                                   Name = gnumbers.Name,
                                   DenyReasonsSet = gnumbers.DenySendingReasons.OrderByDescending(dsr => dsr.DenyReasonID).FirstOrDefault(),
                                   SendAllowed = gnumbers.SendAllowed,
                                   UserId = gnumbers.DenySendingReasons.OrderByDescending(un => un.UserID).FirstOrDefault()
                               }).ToList();
                logger.Info("Numbers are returned from db.");

                //TODO: ?. Elvis operator. To znaci ako je leva strana null onda ce dodeliti null za vrednost

                List<NumbersViewModel> model = new List<NumbersViewModel>();
                
                logger.Info("Client returned by id: " + clientId);
                try
                {
                    //var cookie = Request.Headers.GetCookies("_culture").FirstOrDefault();

                    //Thread.CurrentThread.CurrentCulture = new CultureInfo(cookie["_culture"].Value);
                    //Thread.CurrentThread.CurrentUICulture = new CultureInfo(cookie["_culture"].Value);

                    var cookie = Request.Headers.GetCookies();
                    
                    logger.Info("Get cookie value: " + cookie[0]["_culture"].Value);

                    Thread.CurrentThread.CurrentCulture = new CultureInfo(cookie[0]["_culture"].Value);
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo(cookie[0]["_culture"].Value);
                    logger.Info("Language is set to: " + cookie[0]["_culture"].Value);

                    if (cookie[0]["_culture"] == null || cookie[0]["_culture"].Value == "")
                    {
                        logger.Info("cookie language not set");
                        throw new Exception("LanguageError: cookie language not set");
                    }
                }
                catch
                {
                    Thread.CurrentThread.CurrentCulture = new CultureInfo("sr");
                    Thread.CurrentThread.CurrentUICulture = new CultureInfo("sr");
                    logger.Info("Language manually set to: sr");
                }

                foreach (var number in numbers)
                {
                    string editNumber = "<a href=\"/AdminManage/EditNumber/" + number.NumberID + "\" data-ajax-update=\"#numbers-wraper\" data-ajax-mode=\"replace\" data-ajax-method=\"GET\" data-ajax=\"true\">" + Resources.Resources.Edit + "</a>";
                    string toggleEditNumber = "<a data-number-id=\"" + number.NumberID + "\" href=\"/AdminManage/ToggleLockNumber/" + number.NumberID + "?clientId=" + clientId + "\" class=\"stop-allow-sending\">" + (number.SendAllowed == true ? Resources.Resources.DenySending : Resources.Resources.AllowSending) + "</a>";

                    model.Add(new NumbersViewModel()
                    {
                        NumberID = number.NumberID,
                        NumberType = number.NumberType,
                        Number = number.Number,
                        Name = number.Name,
                        SendAllowed = number.SendAllowed == true ? Resources.Resources.Yes : Resources.Resources.No,
                        DeniedReason = number.DenyReasonsSet != null ? (number.DenyReasonsSet?.Reason == "Dozvoljeno slanje" ? (Resources.Resources.SendAllowed + " | " + number.DenyReasonsSet?.InsertDate.ToString() + " | "/* + Resources.Resources.ReasonByUser + " " */+ number.DenyReasonsSet.User.UserName) : number.DenyReasonsSet?.Reason + " | " + number.DenyReasonsSet?.InsertDate.ToString() + " | " /*+ Resources.Resources.ReasonByUser + " " */+ number.DenyReasonsSet.User.UserName) : String.Empty,
                        EditSection = /*editNumber + " | " +*/ toggleEditNumber
                    }) ;
                }
                logger.Info("Model is created");

                NumbersListViewModel data = new NumbersListViewModel() { data = model };
                
                logger.Info("Data is returned from api");

                return Ok(data);
            }
            catch(System.Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        [HttpPost]
        public void DenySending(DenySendingReason denySendingReason)
        {
            logger.SetControllerAction("AdminManageController:ApiController", "DenySending");
            if (ModelState.IsValid)
            {
                logger.Info("Find number with id: " + denySendingReason.NumberID);
                var number = db.Numbers.Find(denySendingReason.NumberID);

                if (number.SendAllowed == true)
                {
                    logger.Info("Deny sending");
                    if (denySendingReason.Reason == null)
                    {
                        logger.Warn("Reason is empty");
                        throw new HttpException(400, "Bad Request");
                    }
                    else
                    {
                        try
                        {
                            db.DenySendingReason.Add(new DenySendingReasonModel()
                            {
                                InsertDate = DateTime.Now,
                                UserID = User.Identity.GetUserId(),
                                NumberID = denySendingReason.NumberID,
                                Reason = denySendingReason.Reason,
                                SendAllowed = false
                            });
                            db.SaveChanges();
                            logger.Info("Sending denied for NumberId: " + denySendingReason.NumberID + " with reason: " + denySendingReason.Reason);
                        }
                        catch (Exception ex)
                        {
                            logger.Error(ex.ToString());
                            throw new HttpException(400, "Bad Request");
                        }
                    }
                }
                else
                {
                    logger.Info("Allow sending");
                    try
                    {
                        db.DenySendingReason.Add(new DenySendingReasonModel()
                        {
                            InsertDate = DateTime.Now,
                            UserID = User.Identity.GetUserId(),
                            NumberID = denySendingReason.NumberID,
                            Reason = "Dozvoljeno slanje",
                            SendAllowed = true
                        });
                        db.SaveChanges();
                        logger.Info("Sending allowed");
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex.ToString());
                        throw new HttpException(400, "Bad Request");
                    }
                }
            }
        }

        [HttpGet]
        public IHttpActionResult GetClientData(string Id)
        {
            Logger logger = new Logger();
            logger.SetControllerAction("AdminManageController:ApiController", "GetClientData");

            logger.Info("Get client contract data for contractId: " + Id.ToString());

            var dbClient = db.ClientContract.Where(cc => cc.ContractId == Id).FirstOrDefault();//db.Client.Where(c => c.ContractID == Id).FirstOrDefault();

            if (dbClient != null)
            {
                logger.Error("Contract already exist");
                return BadRequest();
            }

            logger.Info("Data successfully returned.");

            return Ok(GetClientDataFromCRM(Id));
        }

        

        [HttpPost]
        public IHttpActionResult ImportNumbers(int Id)
        {
            logger.SetControllerAction("AdminManageController:ApiController", "ImportNumbers");
            logger.Info("Get client data for clientId: " + Id.ToString());
            var client = db.Client.Find(Id);

            var contracts = client.Contracts;
            DateTime defaultDate = new DateTime(1900, 1, 1);
            int count = 0;

            logger.Info("Import/Refresh numbers for all client contracts");
            foreach (var contract in contracts)
            {
                if(contract.SynchronizationDate.Date == defaultDate)
                {
                    count += ImportNewNumbers(contract.ContractId);
                }
                else
                {
                    count += RefreshNumbers(contract.ContractId);
                }
            }

            logger.Info("Import/Refresh numbers count: " + count.ToString());

            return Ok(count.ToString());
        }

        private int RefreshNumbers(string contractId)
        {
            int returnValue = 0;

            using (var conn = new SqlConnection(ConStr))
            using (var command = new SqlCommand("sp_RefreshNumbers", conn))
            {
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@nContractID", SqlDbType.VarChar).Value = contractId;

                SqlParameter returnParameter = new SqlParameter("@iAffectedNumbersCount", SqlDbType.Int);
                returnParameter.Direction = ParameterDirection.Output;
                command.Parameters.Add(returnParameter);

                try
                {
                    conn.Open();
                    command.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {

                    Logger logger = new Logger();
                    logger.SetControllerAction("AdminManageController:ApiController", "RefreshNumbers");
                    logger.Error(ex.Message);
                    throw;
                }

                returnValue = (int)returnParameter.Value;
            };
            //executeDb = db.Database.ExecuteSqlCommand("EXEC dbo.sp_RefreshNumbers {0}", contractId);

            return returnValue;
        }

        private int ImportNewNumbers(string contractId)
        {
            int returnValue = 0;

            using (var conn = new SqlConnection(ConStr))
            using (var command = new SqlCommand("sp_InsertNumbers", conn))
            {
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.Add("@nContractID", SqlDbType.VarChar).Value = contractId;

                SqlParameter returnParameter = new SqlParameter("@iInsertedNumbersCount", SqlDbType.Int);
                returnParameter.Direction = ParameterDirection.Output;
                command.Parameters.Add(returnParameter);
                //executeDb = db.Database.ExecuteSqlCommand("EXEC dbo.sp_InsertNumbers {0}", contractId);
                try
                {
                    conn.Open();
                    command.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {

                    Logger logger = new Logger();
                    logger.SetControllerAction("AdminManageController:ApiController", "ImportNewNumbers");
                    logger.Error(ex.Message);
                    throw;
                }

                returnValue = (int)returnParameter.Value;
            };
            
            return returnValue;
        }

        [HttpGet]
        public IHttpActionResult GetClientContracts(int Id)
        {
            logger.SetControllerAction("AdminManageController:ApiController", "GetClientContracts");
            try
            {
                //var cookie = Request.Headers.GetCookies("_culture").FirstOrDefault();

                //Thread.CurrentThread.CurrentCulture = new CultureInfo(cookie["_culture"].Value);
                //Thread.CurrentThread.CurrentUICulture = new CultureInfo(cookie["_culture"].Value);

                var cookie = Request.Headers.GetCookies();

                Thread.CurrentThread.CurrentCulture = new CultureInfo(cookie[0]["_culture"].Value);
                Thread.CurrentThread.CurrentUICulture = new CultureInfo(cookie[0]["_culture"].Value);

                if (cookie[0]["_culture"] == null || cookie[0]["_culture"].Value == "")
                {
                    throw new Exception("LanguageError: cookie language not set");
                }
            }
            catch
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("sr");
                Thread.CurrentThread.CurrentUICulture = new CultureInfo("sr");
            }

            logger.Info("Get client contracts for clientId: " + Id.ToString());
            var dbClientContracts = db.ClientContract
                .Where(cc => cc.ClientId == Id)
                .Select(cc => new
                {
                    ClientContractId = cc.ClientContractsId,
                    ContractId = cc.ContractId,
                    Edit = "<a href=\"/AdminManage/EditClientContract/" + cc.ClientContractsId + "\">" + Resources.Resources.Edit + "</a>"
                })
                .ToList();

            return Ok(new { data = dbClientContracts });
        }

        #region ClientData
        private ClientData GetClientDataFromCRM(string contractId)
        {
            logger.SetControllerAction("AdminManageController:ApiController", "GetClientDataFromCRM");

            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TIS"].ToString();
            var schemaMigration = System.Configuration.ConfigurationManager.AppSettings["schemaMigration"];

            string SqlQuery = "select mts_id, korisnik, prodajni_ugovor_id from " + schemaMigration + ".bizsms_mig ";
            SqlQuery += "where trim(prodajni_ugovor_id) = '" + contractId.Trim() + "'";

            using (OracleConnection conn = new OracleConnection(connectionString))
            {
                using (OracleCommand cmd = new OracleCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = SqlQuery;

                    try
                    {
                        if (conn.State != ConnectionState.Open)
                            conn.Open();

                        DataSet ds = new DataSet();
                        OracleDataAdapter adapter = new OracleDataAdapter(cmd);

                        adapter.Fill(ds);

                        logger.Info("Data successfully returned with count: " + ds.Tables[0].Rows.Count);

                        return ds.Tables[0]
                            .AsEnumerable()
                            .Select(cd => new ClientData()
                            {
                                MTS_ID = cd["mts_id"].ToString().Trim(),
                                ContractID = contractId,
                                ClientName = cd["korisnik"].ToString().Trim()
                            }).FirstOrDefault();

                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex.Message);

                        return null;
                    }
                }
            }
        }

        private IEnumerable<ImportNumbers>GetClientNumbers(int clientId)
        {
            Logger logger = new Logger();
            string contracts = string.Join("', '", db.ClientContract.Where(cc => cc.ClientId == clientId).Select(cc => cc.ContractId).ToList());
            string connectionString = System.Configuration.ConfigurationManager.ConnectionStrings["TIS"].ToString();
            var schemaMigration = System.Configuration.ConfigurationManager.AppSettings["schemaMigration"];

            string SqlQuery = "select trim(b.mts_id) mts_id, trim(b.korisnik) korisnik, su.prodajni_ugovor_id, ";
            SqlQuery += "'0' || substr(su.mg, 2, 2) || su.broj_telefona publicnr ";
            SqlQuery += "from ftpro.specifikacija_ugovor su, " + schemaMigration + ".bizsms_mig b ";
            SqlQuery += "where su.prodajni_ugovor_id = b.prodajni_ugovor_id ";
            SqlQuery += "and su.status_ugovora = 1 ";
            SqlQuery += "and su.tip_linije = '56' ";
            SqlQuery += "and trim(su.prodajni_ugovor_id) IN ('" + contracts + "')";

            using (OracleConnection conn = new OracleConnection(connectionString))
            {
                using (OracleCommand cmd = new OracleCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = SqlQuery;

                    try
                    {
                        if (conn.State != ConnectionState.Open)
                            conn.Open();

                        DataSet ds = new DataSet();
                        OracleDataAdapter adapter = new OracleDataAdapter(cmd);

                        adapter.Fill(ds);

                        logger.SetControllerAction("AdminManageController", "GetClientNumbers");
                        logger.Info("Data successfully returned with count: " + ds.Tables[0].Rows.Count);

                        return ds.Tables[0]
                            .AsEnumerable()
                            .Select(n => new ImportNumbers()
                            {
                                MTS_ID = n["mts_id"].ToString(),
                                PublicNR = n["publicnr"].ToString()
                            })
                            .ToList();
                    }
                    catch (Exception ex)
                    {
                        logger.SetControllerAction("AdminManageController", "GetClientNumbers");
                        logger.Error(ex.Message);

                        return null;
                    }
                }
            }
        }
        #endregion

        [HttpPost]
        public IHttpActionResult CancelScheduledSMS(int id)
        {
            logger.SetControllerAction("AdminManageController:ApiController", "CancelScheduledSMS");

            try
            {
                ApplicationDbContext db = new ApplicationDbContext();

                var userCancelId = User.Identity.GetUserId();
                var clientID = db.Users.Find(userCancelId).ClientID;

                MessageModel Message = db.Message.Find(id);
                if (Message.Status == 4)
                {
                    return Ok("Message already sent!");
                }
                ScheduledSmsModel HangfireJob = db.ScheduledSms
                                        .Where(hj => hj.MessageID == Message.MessageID)
                                        .FirstOrDefault();

                BackgroundJob.Delete(HangfireJob.HangfireID);
                logger.Info("Canceling scheduled SMS with messageId: " + id + ", by clientId: " + clientID.ToString() + " and user: " + userCancelId);

                Message.Status = (int)MessageStatus.ScheduledSendingCanceled;
                //ukoliko je poruka uspesno otkazana charged mora biti false
                Message.Charged = false;
                db.Entry(Message).State = System.Data.Entity.EntityState.Modified;

                HangfireJob.UserID = userCancelId;
                HangfireJob.CancelDate = DateTime.Now;
                db.Entry(HangfireJob).State = System.Data.Entity.EntityState.Modified;

                db.SaveChanges();

                return Ok(new { message = "OK" });
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        public IHttpActionResult GetMessageStatus(int Id)
        {
            logger.SetControllerAction("AdminManageController:ApiController", "GetMessageStatus");
            try
            {
                ApplicationDbContext db = new ApplicationDbContext();
                var messageStatus = db.Message.Find(Id).Status;
                logger.Info("Return message status for messageId: " + Id.ToString());
                return Ok(messageStatus);
            }
            catch (Exception ex)
            {
                logger.Error(ex.ToString());
                return NotFound();
            }
        }

        public IHttpActionResult GetNumberStatus(int Id)
        {
            logger.SetControllerAction("AdminManageController:ApiController", "GetNumberStatus");
            try
            {
                ApplicationDbContext db = new ApplicationDbContext();
                var numberStatus = db.Numbers.Find(Id).SendAllowed;
                logger.Info("Return number status for numberId: " + Id.ToString());
                return Ok(numberStatus);
            }
            catch (Exception ex)
            {
                logger.Error(ex.ToString());
                return NotFound();
            }
        }
    }
}
