#nullable disable
using System.Security.Claims;
using BizSMS.Web.Data;
using BizSMS.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Web.Controllers.Api
{
    // Zamena za legacy BizSMS.Controllers.API.GroupController (Faza 4 §8.2).
    // Odvojen od MVC GroupController-a (isto ime nije dozvoljeno) — eksplicitne "api/Group" rute.
    // [Authorize] umesto legacy AuthorizeApiUser; /api grana vraća 401/403 preko cookie eventova (Program.cs).
    [ApiController]
    [Authorize(Roles = "User,Client")]
    [Route("api/Group")]
    public class GroupApiController : ControllerBase
    {
        private readonly ApplicationDbContext db;
        private readonly Logger logger = new Logger();

        public GroupApiController(ApplicationDbContext db)
        {
            this.db = db;
        }

        // ClientID iz claim-a (AppClaimsPrincipalFactory) — bez dodatnog upita ka bazi (kao BaseController).
        private int CurrentClientId => int.Parse(User.FindFirstValue("ClientID") ?? "0");

        // GET: /api/Group/GetNumbers/{id} — puni DataTable u Views/Group/Numbers.cshtml
        [HttpGet("GetNumbers/{id?}")]
        public IActionResult GetNumbers(int? id)
        {
            logger.SetControllerAction("GroupApiController", "GetNumbers");

            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var group = db.Group.Find(id.Value);

            if (group == null || group.ClientID != clientID)
                return BadRequest();

            // Za podrazumevanu grupu -> link za izmenu; za ostale -> link za brisanje (js-delete-number).
            // bootbox nije portovan: port koristi plain confirm(), pa data-yes/no/question/error atributi ne trebaju.
            string editLinkTemplate = group.Default
                ? "<a href=\"/Group/EditNumber?groupId={0}&numberId={1}\">Izmeni</a>"
                : "<a href=\"#\" class=\"js-delete-number\" data-group-id=\"{0}\" data-number-id=\"{1}\">Izbriši</a>";

            try
            {
                var raw = (from number in db.Numbers
                           join group_number in db.GroupNumbers on number.NumberID equals group_number.NumberID
                           where group_number.GroupID == id && number.SendAllowed && number.Active
                           select new
                           {
                               group_number.GroupID,
                               group_number.NumberID,
                               Number = number.Number,
                               Name = number.Name,
                               NumberTypeName = number.NumberType.Name
                           }).ToList();

                var numbers = raw.Select(r => new
                {
                    EditLink = string.Format(editLinkTemplate, r.GroupID, r.NumberID),
                    NumberType = r.NumberTypeName,
                    Number = r.Number,
                    Name = r.Name
                }).ToList();

                return Ok(new { data = numbers });
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }

            return NotFound();
        }

        // GET: /api/Group/GetListNumbers — puni DataTable u Views/Group/ListNumbers.cshtml
        [HttpGet("GetListNumbers")]
        public IActionResult GetListNumbers()
        {
            logger.SetControllerAction("GroupApiController", "GetListNumbers");

            int clientID = CurrentClientId;

            try
            {
                var raw = db.Numbers
                    .Where(n => n.ClientID == clientID && n.SendAllowed && n.Active)
                    .Select(n => new
                    {
                        n.NumberID,
                        n.Number,
                        n.Name,
                        NumberTypeName = n.NumberType.Name
                    })
                    .ToList();

                var model = raw.Select(n => new
                {
                    EditLink = $"<a href=\"/Group/EditListNumber/{n.NumberID}\">Izmeni</a>",
                    Number = n.Number,
                    Name = n.Name,
                    NumberType = n.NumberTypeName
                }).ToList();

                return Ok(new { data = model });
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }

            return NotFound();
        }

        // GET: /api/Group/ConfirmUploadNumbers/{id} — Views/Group/CheckUploadedNumbers.cshtml koristi rezultat direktno (ne .data)
        [HttpGet("ConfirmUploadNumbers/{id?}")]
        public IActionResult ConfirmUploadNumbers(int? id)
        {
            logger.SetControllerAction("GroupApiController", "ConfirmUploadNumbers");

            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            if (!db.Group.Any(g => g.GroupID == id && g.ClientID == clientID))
                return BadRequest();

            try
            {
                var tempImport = db.TempImport
                    .Where(t => t.GroupId == id)
                    .Select(t => new
                    {
                        Name = t.Name,
                        Number = t.Number,
                        NumberType = t.NumberType.ToString()
                    })
                    .ToList();

                return Ok(tempImport);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
        }
    }
}
