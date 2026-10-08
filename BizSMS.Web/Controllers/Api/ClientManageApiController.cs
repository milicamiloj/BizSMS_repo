#nullable disable
using System;
using System.Threading.Tasks;
using BizSMS.Web.Data;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Web.Controllers.Api
{
    // Zamena za legacy BizSMS.Controllers.API.ClientManageController.
    // Odvojeno ime klase (kolizija sa MVC ClientManageController) + eksplicitna literalna ruta.
    [ApiController]
    [Authorize(Roles = "Administrator")]
    [Route("api/ClientManage")]
    public class ClientManageApiController : ControllerBase
    {
        private readonly ApplicationDbContext db;
        private readonly UserManager<ApplicationUser> _userManager;

        public ClientManageApiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            this.db = db;
            _userManager = userManager;
        }

        // DELETE /api/ClientManage/Cancel/{id} — meko brisanje korisnika (IsCanceled + IsDeleted)
        [HttpDelete("Cancel/{id}")]
        public async Task<IActionResult> Cancel(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.IsCanceled = true;
            user.IsDeleted = true;
            await _userManager.UpdateAsync(user);

            return Ok();
        }

        // DELETE /api/ClientManage/DeleteCost/{id} — deaktivacija cene poruke (postavlja EndDate)
        [HttpDelete("DeleteCost/{id?}")]
        public IActionResult DeleteCost(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }

            var messageCost = db.MessageCost.Find(id);
            if (messageCost == null)
            {
                return NotFound();
            }

            messageCost.EndDate = DateTime.Now;
            db.SaveChanges();

            return Ok();
        }
    }
}
