#nullable disable
using System.Security.Claims;
using BizSMS.Web.Data;
using BizSMS.Web.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Web.Controllers.Api
{
    // Zamena za legacy BizSMS.Controllers.API.UserController (Faza 4 §8.2) — brisanje grupe i brisanje broja iz grupe.
    // Odvojen od (neportovanog) MVC UserController-a; eksplicitne "api/user" rute koje JS već poziva.
    [ApiController]
    [Authorize(Roles = "User,Client")]
    [Route("api/user")]
    public class UserApiController : ControllerBase
    {
        private readonly ApplicationDbContext db;
        private readonly Logger logger = new Logger();

        public UserApiController(ApplicationDbContext db)
        {
            this.db = db;
        }

        // ClientID iz claim-a (AppClaimsPrincipalFactory) — bez dodatnog upita ka bazi (kao BaseController).
        private int CurrentClientId => int.Parse(User.FindFirstValue("ClientID") ?? "0");

        // DELETE: /api/user/deletegroup/{id} — Views/Group/Index.cshtml (js-delete-group)
        [HttpDelete("deletegroup/{id?}")]
        public IActionResult DeleteGroup(int? id)
        {
            logger.SetControllerAction("UserApiController", "DeleteGroup");

            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var group = db.Group.Find(id.Value);

            // Podrazumevana grupa se ne briše; tuđa grupa se ne briše.
            if (group == null || group.ClientID != clientID || group.Default)
                return BadRequest();

            db.Group.Remove(group);
            db.SaveChanges();

            return Ok();
        }

        // DELETE: /api/user/removenumberfromgroup — Views/Group/Numbers.cshtml (js-delete-number)
        // jQuery $.ajax({data:{GroupID,NumberID}, method:"DELETE"}) šalje urlencoded telo -> [FromForm].
        [HttpDelete("removenumberfromgroup")]
        public IActionResult RemoveNumberFromGroup([FromForm] int? GroupID, [FromForm] int? NumberID)
        {
            logger.SetControllerAction("UserApiController", "RemoveNumberFromGroup");

            if (GroupID == null || NumberID == null)
                return BadRequest();

            int clientID = CurrentClientId;
            // Kompozitni ključ GroupNumberModel: (GroupID, NumberID) — vidi ApplicationDbContext.HasKey.
            var group_number = db.GroupNumbers.Find(GroupID, NumberID);

            if (group_number == null || group_number.Numbers.ClientID != clientID || group_number.Groups.Default)
                return BadRequest();

            db.GroupNumbers.Remove(group_number);
            db.SaveChanges();

            return Ok();
        }
    }
}
