using BizSMS.Attributes;
using BizSMS.Models;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace BizSMS.Controllers.API
{
    [AuthorizeApiUser]
    public class UserController : ApiController
    {
        [HttpDelete]
        public IHttpActionResult DeleteGroup(int? id)
        {
            ApplicationDbContext db = new ApplicationDbContext();

            if (id == null)
            {
                return BadRequest();
            }

            var userID = User.Identity.GetUserId();
            var clientID = db.Users.Where(user => user.Id == userID).FirstOrDefault().ClientID;
            var group = db.Group.Find(id);

            if (group.ClientID != clientID || group.Default)
            {
                return BadRequest();
            }

            db.Group.Remove(group);
            db.SaveChanges();

            return Ok();
        }

        public class GroupNumber
        {
            public int? GroupID { get; set; }
            public int? NumberID { get; set; }
        }

        [HttpDelete]
        public IHttpActionResult RemoveNumberFromGroup(GroupNumber GN)
        {
            ApplicationDbContext db = new ApplicationDbContext();

            if (GN.NumberID == null || GN.GroupID == null)
            {
                return BadRequest();
            }

            var userID = User.Identity.GetUserId();
            var clientID = db.Users.Where(user => user.Id == userID).FirstOrDefault().ClientID;
            var group_number = db.GroupNumbers.Find(GN.GroupID, GN.NumberID);

            if (group_number.Numbers.ClientID != clientID || group_number.Groups.Default)
            {
                return BadRequest();
            }

            db.GroupNumbers.Remove(group_number);
            db.SaveChanges();

            return Ok();
        }
    }
}
