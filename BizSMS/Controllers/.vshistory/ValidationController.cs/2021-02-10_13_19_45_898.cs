using BizSMS.Helpers;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace BizSMS.Controllers
{
    [Authorize]
    public class ValidationController : BaseController
    {        
        [HttpPost]
        public JsonResult CheckUsername(string Username, string initialUsername)
        {
            if (initialUsername == null)
            {
                //var user = UserManager.FindByName(Username);
                //ukoliko se u donjem redu ostavi !u.IsDeleted onda validacija preskace user-e gde je Is_Deleted = 1 i baca gresku jer je kolona user u bazi setovana na unique
                var user = db.Users.Where(u => u.UserName == Username /*&& !u.IsDeleted*/).FirstOrDefault();
                return Json(user == null);
            }
            else if(Username != initialUsername)
            {
                var user = UserManager.FindByName(Username);
                return Json(user == null);
            }

            return Json(true);
        }

        [HttpPost]
        public JsonResult CheckEmail(string Email, string initialEmail)
        {
            if (initialEmail == null)
            {
                var user = UserManager.FindByEmail(Email);
                return Json(user == null);
            }
            else if (Email != initialEmail)
            {
                var user = UserManager.FindByEmail(Email);
                return Json(user == null);
            }

            return Json(true);
        }

        [HttpPost]
        public JsonResult CheckClientname(string ClientName, string initialClientName)
        {
            if (initialClientName == null)
            {
                var client = db.Client.Where(c => c.Name == ClientName).FirstOrDefault();
                return Json(client == null);
            }
            else if (ClientName != initialClientName)
            {
                var client = db.Client.Where(c => c.Name == ClientName).FirstOrDefault();
                return Json(client == null);
            }

            return Json(true);
        }

        [HttpPost]
        public JsonResult CheckMTSID(string Mts_ID, string InitialMtsId)
        {
            if (InitialMtsId == null)
            {
                var client = db.Client.Where(c => c.MtsID == Mts_ID).FirstOrDefault();
                return Json(client == null);
            }
            else if (Mts_ID != InitialMtsId)
            {
                var client = db.Client.Where(c => c.MtsID == Mts_ID).FirstOrDefault();
                return Json(client == null);
            }

            return Json(true);
        }

        [HttpPost]
        public JsonResult CheckContractID(string ContractID, string InitialContractId)
        {
            if (InitialContractId == null)
            {
                var contract = db.ClientContract.Where(cn => cn.ContractId == ContractID).FirstOrDefault();
                return Json(contract == null);
            }
            else if (ContractID != InitialContractId)
            {
                var contract = db.ClientContract.Where(cn => cn.ContractId == ContractID).FirstOrDefault();
                return Json(contract == null);
            }

            return Json(true);
        }

        //[HttpPost]
        //public JsonResult CheckAlphanumeric(string Alphanumeric, string InitialAlphanumeric)
        //{
        //    if (InitialAlphanumeric == null)
        //    {
        //        var alphanumeric = db.Alphanumeric.Where(a => a.Alphanumeric == Alphanumeric).FirstOrDefault();
        //        return Json(alphanumeric == null);
        //    }
        //    else if (Alphanumeric != InitialAlphanumeric)
        //    {
        //        var alphanumeric = db.Alphanumeric.Where(a => a.Alphanumeric == Alphanumeric).FirstOrDefault();
        //        return Json(alphanumeric == null);
        //    }

        //    return Json(true);
        //}

        [HttpPost]
        public JsonResult NumberExist(string Number, string InitialNumber, int ClientID)
        {
            if (InitialNumber == null || Number != InitialNumber)
            {
                var numberExists = db.Numbers.Where(number => number.Number == Number && number.Active == true &&
                (number.ClientID == ClientID || (number.ClientID != ClientID && number.NumberTypeID == (int)Helpers.NumberType.VPN)))
                    .FirstOrDefault();

                return Json(numberExists == null);
            }

            return Json(true);
        }

        [HttpPost]
        public JsonResult NumberExistInGroup(string Number, int GroupID)
        {
           int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

                var numberExistInGroup = db.GroupNumbers.Where(
                    number =>
                    number.GroupID == GroupID &&
                    number.NumberID == (db.Numbers.Where(
                    nm =>
                    nm.Number == Number && nm.ClientID == clientID
                )
                .FirstOrDefault()).NumberID
                    )
                    .FirstOrDefault();

            if (numberExistInGroup != null)
            {
                return Json(false);
            }
            else 
            {
                return Json(true);

            }
        }

        [HttpPost]
        public JsonResult NumberTypeCantBeInGroup(int numberTypeID, int groupID)
        {
            var groupName = db.Group.Find(groupID).Name;
            if ((numberTypeID == (int)NumberType.VPN && groupName != "VPN") || (numberTypeID != (int)NumberType.VPN && groupName == "VPN"))
            {
                return Json(false);
            }

            return Json(true);
        }
    }
}