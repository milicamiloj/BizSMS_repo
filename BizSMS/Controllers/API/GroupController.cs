using BizSMS.Models;
using BizSMS.Attributes;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Web.Http;
using System.Collections.Generic;
using System;
using BizSMS.Helpers;

namespace BizSMS.Controllers.API
{
    [AuthorizeApiUser]
    public class GroupController : ApiController
    {
        Logger logger = new Logger();

        private ApplicationDbContext _db;

        private ApplicationUserManager _userManager;

        public GroupController()
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

        [HttpGet]
        public IHttpActionResult GetNumbers(int? id)
        {
            logger.SetControllerAction("GroupController:ApiController", "GetNumbers");

            try
            {
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

            if (id == null)
            {
                return BadRequest();
            }

            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var group = db.Group.Find(id);

            if (group == null || group.ClientID != clientID)
            {
                return BadRequest();
            }

            string editLinkTemplate = "";
            if (!group.Default)
            {
                editLinkTemplate = "<a href=# class=\"js-delete-number\" data-yes=\"" + Resources.Resources.Yes + "\"" +
                    "data-no=\"" + Resources.Resources.No + "\"" +
                    "data-question=\"" + Resources.Resources.QuestionDeleteNumber + "\"" +
                    "data-error=\"" + Resources.Resources.ErrorContactAdmin + "\"" +
                    "data-group-id=\"{0}\" data-number-id=\"{1}\">" + Resources.Resources.Remove + "</a>";
            }
            else
            {
                editLinkTemplate = "<a href=\"/Group/EditNumber?groupId={0}&numberId={1}\">" + Resources.Resources.Edit + "</a>";
            }

            try
            {
                var model = (from number in db.Numbers
                             join group_number in db.GroupNumbers on number.NumberID equals group_number.NumberID
                             where group_number.GroupID == id && number.SendAllowed && number.Active
                             select new
                             {
                                 Number = number,
                                 GroupNumber = group_number
                             }).ToList();

                var numbers = model.AsEnumerable().Select(modelData => new
                {
                    EditLink = string.Format(editLinkTemplate, modelData.GroupNumber.GroupID, modelData.GroupNumber.NumberID),
                    NumberType = modelData.Number.NumberType.Name,
                    Number = modelData.Number.Number,
                    Name = modelData.Number.Name
                }).ToList();

                var data = new { data = numbers };

                return Ok(data);
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
            }

            return NotFound();
        }

        [HttpGet]
        public IHttpActionResult GetListNumbers()
        {
            logger.SetControllerAction("GroupController:ApiController", "GetListNumbers");

            try
            {
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

            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

            try
            {
                var model = db.Numbers
                    .Where(n => n.ClientID == clientID && n.SendAllowed && n.Active)
                    .Select(n => new 
                    {
                        EditLink = "<a href=\"/Group/EditListNumber/" + n.NumberID + "\">" + Resources.Resources.Edit + "</a>",
                        Number = n.Number,
                        Name = n.Name,
                        NumberType = n.NumberType.Name
                    })
                    .ToList();

                var data = new { data = model };

                return Ok(data);
            }
            catch(Exception ex)
            {
                logger.Error(ex.Message);
            }

            return NotFound();
        }

        [HttpGet]
        public IHttpActionResult ConfirmUploadNumbers(int? id)
        {
            logger.SetControllerAction("GroupController:ApiController", "ConfirmUploadNumbers");

            List<ClientTempImportUpload> tempImport = null;
            try
            {
                tempImport = db.TempImport.Where(t => t.GroupId == id)
                                          .Select(t => new ClientTempImportUpload
                                          {
                                              Name = t.Name,
                                              Number = t.Number,
                                              NumberType = t.NumberType.ToString()
                                          }).ToList();
            }
            catch(Exception ex)
            {
                logger.Error(ex.Message);
                return NotFound();
            }
            //Ukoliko se Ajax poziv radi u okviru DataTable (BizSMS\Views\Group\CheckUploadedNumbers.cshtml) onda odkomentarisati red ispod
            //ClientTempImportData data = new ClientTempImportData() { data = tempImport };
            return Ok(tempImport);
        }

    }
}
