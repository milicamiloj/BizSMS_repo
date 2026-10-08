#nullable disable
using System.Data;
using BizSMS.Web.Data;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Web.Controllers.Api
{
    // Zamena za legacy BizSMS.Controllers.API.AdminManageController.
    // Odvojeno ime klase (kolizija sa MVC) + eksplicitna literalna ruta api/AdminManage.
    // ODLOŽENO za Fazu 5: GetClientData (Oracle/TIS CRM), CancelScheduledSMS (Hangfire),
    // GetClientNumbers (Oracle, u legacy-ju je bio i neupotrebljen).
    [ApiController]
    [Authorize(Roles = "Administrator")]
    [Route("api/AdminManage")]
    public class AdminManageApiController : ControllerBase
    {
        private readonly ApplicationDbContext db;
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminManageApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            this.db = db;
            _userManager = userManager;
        }

        // DELETE /api/AdminManage/Cancel/{id} — meko brisanje korisnika
        [HttpDelete("Cancel/{id}")]
        public async Task<IActionResult> Cancel(string id)
        {
            if (string.IsNullOrEmpty(id))
                return BadRequest();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            user.IsCanceled = true;
            user.IsDeleted = true;
            await _userManager.UpdateAsync(user);

            return Ok();
        }

        // DELETE /api/AdminManage/DeleteAlphanumeric/{id}
        [HttpDelete("DeleteAlphanumeric/{id}")]
        public IActionResult DeleteAlphanumeric(int id)
        {
            var model = db.Alphanumeric.Find(id);
            if (model == null)
                return NotFound();

            db.Alphanumeric.Remove(model);
            db.SaveChanges();

            return Ok();
        }

        // GET /api/AdminManage/GetNumbers?clientId= — brojevi klijenta za DataTables
        [HttpGet("GetNumbers")]
        public IActionResult GetNumbers([FromQuery] int? clientId)
        {
            if (clientId == null)
                return NotFound();

            try
            {
                // Jedan projektovani upit umesto lazy-load N+1: NumberType, poslednji
                // DenySendingReason i njegov User se povlače join-ovima/subquery-jem u bazi.
                var rows = db.Numbers
                    .Where(n => n.ClientID == clientId && n.Active)
                    .Select(n => new
                    {
                        n.NumberID,
                        NumberType = n.NumberType.Name,
                        n.Number,
                        n.Name,
                        n.SendAllowed,
                        LastReason = n.DenySendingReasons
                            .OrderByDescending(dsr => dsr.DenyReasonID)
                            .Select(dsr => new { dsr.Reason, dsr.InsertDate, UserName = dsr.User.UserName })
                            .FirstOrDefault()
                    })
                    .ToList();

                List<NumbersViewModel> model = new List<NumbersViewModel>();

                foreach (var row in rows)
                {
                    string toggleLink = "<a data-number-id=\"" + row.NumberID +
                        "\" href=\"/AdminManage/ToggleLockNumber/" + row.NumberID + "?clientId=" + clientId +
                        "\" class=\"stop-allow-sending\">" + (row.SendAllowed ? "Zabrani slanje" : "Dozvoli slanje") + "</a>";

                    string deniedReason = "";
                    if (row.LastReason != null)
                    {
                        string reasonText = row.LastReason.Reason == "Dozvoljeno slanje" ? "Dozvoljeno slanje" : row.LastReason.Reason;
                        deniedReason = reasonText + " | " + row.LastReason.InsertDate.ToString() + " | " + row.LastReason.UserName;
                    }

                    model.Add(new NumbersViewModel()
                    {
                        NumberID = row.NumberID,
                        NumberType = row.NumberType,
                        Number = row.Number,
                        Name = row.Name,
                        SendAllowed = row.SendAllowed ? "Da" : "Ne",
                        DeniedReason = deniedReason,
                        EditSection = toggleLink
                    });
                }

                return Ok(new NumbersListViewModel { data = model });
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // POST /api/AdminManage/DenySending — zabrani/dozvoli slanje na broj (razlog)
        [HttpPost("DenySending")]
        public IActionResult DenySending([FromForm] DenySendingReason denySendingReason)
        {
            var number = db.Numbers.Find(denySendingReason.NumberID);
            if (number == null)
                return NotFound();

            string userId = _userManager.GetUserId(User);

            if (number.SendAllowed)
            {
                if (string.IsNullOrWhiteSpace(denySendingReason.Reason))
                    return BadRequest();

                db.DenySendingReason.Add(new DenySendingReasonModel()
                {
                    InsertDate = DateTime.Now,
                    UserID = userId,
                    NumberID = denySendingReason.NumberID,
                    Reason = denySendingReason.Reason,
                    SendAllowed = false
                });
            }
            else
            {
                db.DenySendingReason.Add(new DenySendingReasonModel()
                {
                    InsertDate = DateTime.Now,
                    UserID = userId,
                    NumberID = denySendingReason.NumberID,
                    Reason = "Dozvoljeno slanje",
                    SendAllowed = true
                });
            }

            db.SaveChanges();
            return Ok();
        }

        // POST /api/AdminManage/ImportNumbers/{id} — uvoz/osvežavanje brojeva za sve ugovore klijenta (SP sa output count-om)
        [HttpPost("ImportNumbers/{id}")]
        public IActionResult ImportNumbers(int id)
        {
            var client = db.Client.Find(id);
            if (client == null)
                return NotFound();

            var contracts = client.Contracts;
            DateTime defaultDate = new DateTime(1900, 1, 1);
            int count = 0;

            foreach (var contract in contracts)
            {
                if (contract.SynchronizationDate.Date == defaultDate)
                {
                    count += ImportNewNumbers(contract.ContractId);
                }
                else
                {
                    count += RefreshNumbers(contract.ContractId);
                }
            }

            return Ok(count.ToString());
        }

        // GET /api/AdminManage/GetClientContracts/{id} — ugovori klijenta za DataTables
        [HttpGet("GetClientContracts/{id}")]
        public IActionResult GetClientContracts(int id)
        {
            var dbClientContracts = db.ClientContract
                .Where(cc => cc.ClientId == id)
                .Select(cc => new
                {
                    ClientContractId = cc.ClientContractsId,
                    ContractId = cc.ContractId,
                    Edit = "<a href=\"/AdminManage/EditClientContract/" + cc.ClientContractsId + "\">Izmeni</a>"
                })
                .ToList();

            return Ok(new { data = dbClientContracts });
        }

        // GET /api/AdminManage/GetNumberStatus/{id}
        [HttpGet("GetNumberStatus/{id}")]
        public IActionResult GetNumberStatus(int id)
        {
            try
            {
                var numberStatus = db.Numbers.Find(id).SendAllowed;
                return Ok(numberStatus);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        // GET /api/AdminManage/GetMessageStatus/{id}
        [HttpGet("GetMessageStatus/{id}")]
        public IActionResult GetMessageStatus(int id)
        {
            try
            {
                var messageStatus = db.Message.Find(id).Status;
                return Ok(messageStatus);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }

        private readonly Helpers.Logger logger = new Helpers.Logger();

        private int RefreshNumbers(string contractId)
        {
            return ExecuteCountingSp("sp_RefreshNumbers", "@nContractID", contractId, "@iAffectedNumbersCount");
        }

        private int ImportNewNumbers(string contractId)
        {
            return ExecuteCountingSp("sp_InsertNumbers", "@nContractID", contractId, "@iInsertedNumbersCount");
        }

        // Poziva SP nad postojećom (DI) konekcijom i vraća vrednost output parametra.
        private int ExecuteCountingSp(string spName, string inParamName, string contractId, string outParamName)
        {
            var connection = (SqlConnection)db.Database.GetDbConnection();
            bool wasClosed = connection.State != ConnectionState.Open;

            using var command = new SqlCommand(spName, connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.Add(inParamName, SqlDbType.VarChar).Value = contractId;

            var outParam = new SqlParameter(outParamName, SqlDbType.Int) { Direction = ParameterDirection.Output };
            command.Parameters.Add(outParam);

            try
            {
                if (wasClosed)
                    connection.Open();

                command.ExecuteNonQuery();

                return outParam.Value == DBNull.Value ? 0 : (int)outParam.Value;
            }
            catch (SqlException ex)
            {
                logger.Error(ex.Message);
                throw;
            }
            finally
            {
                if (wasClosed && connection.State == ConnectionState.Open)
                    connection.Close();
            }
        }
    }
}
