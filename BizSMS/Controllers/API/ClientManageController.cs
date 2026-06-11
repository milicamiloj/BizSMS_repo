using BizSMS.Attributes;
using BizSMS.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace BizSMS.Controllers.API
{
    [AuthorizeApiUser(Roles = "Administrator")]
    public class ClientManageController : ApiController
    {
        private ApplicationDbContext _context;
        private ApplicationUserManager _userManager;

        public ClientManageController()
        {
            context = new ApplicationDbContext();
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? Request.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        public ApplicationDbContext context
        {
            get
            {
                return _context;
            }
            private set
            {
                _context = value;
            }
        }

        [HttpDelete]
        public IHttpActionResult Cancel(string id)
        {
            if(string.IsNullOrEmpty(id))
            {
                return BadRequest();
            }
            var user = UserManager.FindById(id);

            if(user == null)
            {
                return NotFound();
            }

            user.IsCanceled = true;
            user.IsDeleted = true;

            UserManager.Update(user);
            context.SaveChanges();

            return Ok();
        }

        [HttpDelete]
        public IHttpActionResult DeleteCost(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }

            var messageCost = context.MessageCost.Find(id);

            if(messageCost == null)
            {
                return NotFound();
            }

            messageCost.EndDate = DateTime.Now;
            context.SaveChanges();

            return Ok();
        }
    }
}
