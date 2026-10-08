using System.Threading.Tasks;
using BizSMS.Web.Data;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Web.Controllers
{
    // Zamena za legacy BizSMS.Controllers.ValidationController (§7/§8).
    // AJAX validacioni endpointi (jedinstvenost username/email/klijent/ugovor, provera brojeva).
    // Napomena: legacy je koristio [Remote] atribute na view-modelima; oni su izostavljeni pri portu
    // (server-side provere u akcijama pokrivaju), pa se ovi endpointi zovu ručno iz JS-a po potrebi.
    [Authorize]
    public class ValidationController : BaseController
    {
        public ValidationController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
            : base(db, userManager)
        {
        }

        [HttpPost]
        public IActionResult CheckUsername(string Username, string initialUsername)
        {
            if (initialUsername == null)
            {
                // Namerno se NE filtrira IsDeleted: kolona UserName je unique u bazi, pa bi preskakanje
                // obrisanih korisnika dozvolilo upis duplikata i pucanje na insertu.
                var user = db.Users.FirstOrDefault(u => u.UserName == Username);
                return Json(user == null);
            }
            else if (Username != initialUsername)
            {
                var user = db.Users.FirstOrDefault(u => u.UserName == Username);
                return Json(user == null);
            }

            return Json(true);
        }

        [HttpPost]
        public async Task<IActionResult> CheckEmail(string Email, string initialEmail)
        {
            if (initialEmail == null || Email != initialEmail)
            {
                var user = await UserManager.FindByEmailAsync(Email);
                return Json(user == null);
            }

            return Json(true);
        }

        [HttpPost]
        public IActionResult CheckClientname(string ClientName, string initialClientName)
        {
            if (initialClientName == null || ClientName != initialClientName)
            {
                var client = db.Client.FirstOrDefault(c => c.Name == ClientName);
                return Json(client == null);
            }

            return Json(true);
        }

        [HttpPost]
        public IActionResult CheckMTSID(string Mts_ID, string InitialMtsId)
        {
            if (InitialMtsId == null || Mts_ID != InitialMtsId)
            {
                var client = db.Client.FirstOrDefault(c => c.MtsID == Mts_ID);
                return Json(client == null);
            }

            return Json(true);
        }

        [HttpPost]
        public IActionResult CheckContractID(string ContractID, string InitialContractId)
        {
            if (InitialContractId == null || ContractID != InitialContractId)
            {
                var contract = db.ClientContract.FirstOrDefault(cn => cn.ContractId == ContractID);
                return Json(contract == null);
            }

            return Json(true);
        }

        [HttpPost]
        public IActionResult NumberExist(string Number, string InitialNumber, int ClientID)
        {
            if (InitialNumber == null || Number != InitialNumber)
            {
                var numberExists = db.Numbers.FirstOrDefault(number =>
                    number.Number == Number && number.Active == true &&
                    (number.ClientID == ClientID ||
                     (number.ClientID != ClientID && number.NumberTypeID == (int)NumberType.VPN)));

                return Json(numberExists == null);
            }

            return Json(true);
        }

        [HttpPost]
        public IActionResult NumberExistInGroup(string Number, int GroupID)
        {
            int clientID = CurrentClientId;

            // Traži isti aktivan broj u grupi; ako postoji ne dozvoljava upis (dozvoljava upis
            // deaktiviranog VPN broja, kao i legacy).
            var numberInGroup = db.GroupNumbers.Where(gn => gn.GroupID == GroupID)
                .Join(db.Numbers.Where(n => n.Number == Number && n.ClientID == clientID && n.Active),
                    gnums => gnums.NumberID,
                    nums => nums.NumberID,
                    (gnums, nums) => new { GroupNums = gnums, Numbers = nums }).FirstOrDefault();

            return Json(numberInGroup == null);
        }

        [HttpPost]
        public IActionResult NumberTypeCantBeInGroup(int numberTypeID, int groupID)
        {
            var groupName = db.Group.Find(groupID)?.Name;
            if ((numberTypeID == (int)NumberType.VPN && groupName != "VPN") ||
                (numberTypeID != (int)NumberType.VPN && groupName == "VPN"))
            {
                return Json(false);
            }

            return Json(true);
        }

        [HttpPost]
        public IActionResult LimitNumberOfCharacters(string nameNumber)
        {
            // Max 30 karaktera — legacy je preko 30 vraćao EntityValidationErrors.
            return Json(nameNumber.Length <= 30);
        }
    }
}
