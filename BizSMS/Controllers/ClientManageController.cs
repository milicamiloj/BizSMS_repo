using BizSMS.Attributes;
using BizSMS.Models;
using Microsoft.AspNet.Identity;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace BizSMS.Controllers
{
    [AuthorizeUser(Roles = "Administrator")]
    public class ClientManageController : BaseController
    {
        // GET: /ClientManage/ClientManageUsers
        public ActionResult ClientManageUsers(ManageMessageId? message)
        {
            ViewBag.StatusMessage = message == ManageMessageId.ChangePasswordSuccess ? Resources.Resources.PasswordChangedMessage : "";

            List<ClientManageUsersViewModel> model = new List<ClientManageUsersViewModel>();
            var userID = User.Identity.GetUserId();
            var clientID = db.Users.Where(user => user.Id == userID).FirstOrDefault().ClientID;
            
            foreach (var user in db.Users)
            {
                if (user.ClientID == clientID && userID != user.Id && !user.IsCanceled)
                {
                    model.Add(new ClientManageUsersViewModel
                    {
                        UserID = user.Id,
                        Email = user.Email,
                        PhoneNumber = user.PhoneNumber
                    });
                }
            }            

            return View(model);
        }

        //GET: /ClientManage/ClientCreateUser
        public ActionResult ClientCreateUser()
        {
            return View();
        }

        // POST: /ClientManage/ClientCreateUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClientCreateUser(ClientCreateUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = UserManager.FindByEmail(model.Email);
                var curentUser = UserManager.FindById(User.Identity.GetUserId());
                                
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = model.Email, Email = model.Email,
                        ClientID = curentUser.ClientID, PhoneNumber = model.PhoneNumber
                    };

                    var result = UserManager.Create(user, model.Password);
                    result = UserManager.SetLockoutEnabled(user.Id, false);

                    var roles = UserManager.AddToRole(user.Id, "User");
                }
                else if(user.IsCanceled)
                {
                    user.IsCanceled = false;
                    user.IsDeleted = false;
                    user.PasswordHash = UserManager.PasswordHasher.HashPassword(model.Password);
                    UserManager.Update(user);
                }

                return RedirectToAction("ClientManageUsers");
            }
            return View(model);
        }

        //GET: /ClientManage/ClientEditUser/5
        public ActionResult ClientEditUser(string id)
        {
            if (id == null)
            {
                throw new HttpException(400, "Bad Request");
            }

            var user = db.Users.Find(id);

            if (user == null)
            {
                throw new HttpException(404, "Not Found");
            }

            ClientEditUserViewModel model = new ClientEditUserViewModel
            {
                UserID = id,
                Username = user.UserName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber
            };

            return View(model);
        }

        //POST: /ClientManage/ClientEditUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ClientEditUser(ClientEditUserViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = UserManager.FindById(model.UserID);
                user.PhoneNumber = model.PhoneNumber;
                UserManager.Update(user);

                db.SaveChanges();

                return RedirectToAction("ClientManageUsers");
            }

            return View(model);
        }

        public enum ManageMessageId
        {
            AddPhoneSuccess,
            ChangePasswordSuccess,
            SetTwoFactorSuccess,
            SetPasswordSuccess,
            RemoveLoginSuccess,
            RemovePhoneSuccess,
            Error
        }
    }
}