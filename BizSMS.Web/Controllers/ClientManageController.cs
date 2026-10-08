using System.Linq;
using System.Threading.Tasks;
using BizSMS.Web.Data;
using BizSMS.Web.Filters;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Web.Controllers
{
    // Zamena za legacy BizSMS.Controllers.ClientManageController.
    // Administrator upravlja korisnicima svog klijenta (lista/kreiranje/izmena).
    [AuthorizeUser(Roles = "Administrator")]
    public class ClientManageController : BaseController
    {
        public ClientManageController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
            : base(db, userManager)
        {
        }

        // GET: /ClientManage/ClientManageUsers
        public IActionResult ClientManageUsers(ManageMessageId? message)
        {
            ViewBag.StatusMessage = message == ManageMessageId.ChangePasswordSuccess ? "Lozinka je uspešno promenjena." : "";

            var userId = UserManager.GetUserId(User);
            int clientID = CurrentClientId;

            var model = db.Users
                .Where(u => u.ClientID == clientID && u.Id != userId && !u.IsCanceled)
                .Select(u => new ClientManageUsersViewModel
                {
                    UserID = u.Id,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber
                })
                .ToList();

            return View(model);
        }

        // GET: /ClientManage/ClientCreateUser
        public IActionResult ClientCreateUser()
        {
            return View();
        }

        // POST: /ClientManage/ClientCreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClientCreateUser(ClientCreateUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await UserManager.FindByEmailAsync(model.Email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    ClientID = CurrentClientId,
                    PhoneNumber = model.PhoneNumber
                };

                await UserManager.CreateAsync(user, model.Password);
                await UserManager.SetLockoutEnabledAsync(user, false);
                await UserManager.AddToRoleAsync(user, "User");
            }
            else if (user.IsCanceled)
            {
                // Reaktivacija ranije otkazanog naloga sa novom lozinkom.
                user.IsCanceled = false;
                user.IsDeleted = false;
                user.PasswordHash = UserManager.PasswordHasher.HashPassword(user, model.Password);
                await UserManager.UpdateAsync(user);
            }

            return RedirectToAction("ClientManageUsers");
        }

        // GET: /ClientManage/ClientEditUser/{id}
        public async Task<IActionResult> ClientEditUser(string id)
        {
            if (id == null)
            {
                return BadRequest();
            }

            var user = await UserManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var model = new ClientEditUserViewModel
            {
                UserID = id,
                Username = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        // POST: /ClientManage/ClientEditUser/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClientEditUser(ClientEditUserViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await UserManager.FindByIdAsync(model.UserID);
            if (user == null)
            {
                return NotFound();
            }

            user.PhoneNumber = model.PhoneNumber;
            await UserManager.UpdateAsync(user);

            return RedirectToAction("ClientManageUsers");
        }
    }
}
