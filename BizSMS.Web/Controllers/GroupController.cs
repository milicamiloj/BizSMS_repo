#nullable disable
using System.Data;
using System.Text.RegularExpressions;
using BizSMS.Web.Data;
using BizSMS.Web.Filters;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using BizSMS.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace BizSMS.Web.Controllers
{
    [AuthorizeUser(Roles = "User,Client")]
    public class GroupController : BaseController
    {
        private readonly ILdapChecker _ldap;
        private readonly IWebHostEnvironment _env;

        public GroupController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ILdapChecker ldap,
            IWebHostEnvironment env)
            : base(db, userManager)
        {
            _ldap = ldap;
            _env = env;
        }

        private string UploadedFilesPath => Path.Combine(_env.ContentRootPath, "UploadedFiles");

        // GET: Group
        public IActionResult Index()
        {
            int clientID = CurrentClientId;

            var modelQry = (from groups in db.Group
                            join group_numbers in db.GroupNumbers on groups.GroupID equals group_numbers.GroupID into group_number
                            where groups.ClientID == clientID
                            from gn in group_number.DefaultIfEmpty()
                            group gn by groups.GroupID into grouped
                            select new
                            {
                                GroupID = grouped.Key,
                                TotalOfNumbers = grouped.Count(t => t.NumberID != 0 && t.Numbers.SendAllowed && t.Numbers.Active)
                            }).ToList();

            List<UserManageGroupsViewModel> model = new List<UserManageGroupsViewModel>();

            foreach (var m in modelQry)
            {
                var group = db.Group.Find(m.GroupID);

                model.Add(new UserManageGroupsViewModel()
                {
                    GroupID = m.GroupID,
                    isDefault = group.Default,
                    Name = group.Name,
                    TotalOfNumbers = m.TotalOfNumbers
                });
            }

            return View(model);
        }

        //GET: /Group/CreateGroup
        public IActionResult CreateGroup()
        {
            return View();
        }

        //POST: /Group/CreateGroup
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateGroup(UserCreateGroupViewModel model)
        {
            if (ModelState.IsValid)
            {
                int clientID = CurrentClientId;

                GroupModel group = new GroupModel()
                {
                    ClientID = clientID,
                    Name = model.Name,
                    Default = false,
                    InsertDate = DateTime.Now
                };

                db.Group.Add(group);
                db.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(model);
        }

        //GET: /Group/EditGroup/{GroupID}
        public IActionResult EditGroup(int? id)
        {
            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var group = db.Group.Find(id.Value);

            if (group == null || group.ClientID != clientID || group.Default)
                return BadRequest();

            UserEditGroupViewModel model = new UserEditGroupViewModel()
            {
                GroupID = group.GroupID,
                Name = group.Name
            };

            return View(model);
        }

        //POST: /Group/EditGroup/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditGroup(UserEditGroupViewModel model)
        {
            if (ModelState.IsValid)
            {
                var group = db.Group.Find(model.GroupID);
                int clientID = CurrentClientId;

                if (group == null || group.ClientID != clientID)
                    return StatusCode(StatusCodes.Status403Forbidden);

                group.Name = model.Name;
                group.InsertDate = DateTime.Now;
                db.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(model);
        }

        //GET: /Group/Numbers/{GroupID}
        public IActionResult Numbers(int? id)
        {
            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var groupId = db.Group.Find(id.Value);

            if (groupId == null || groupId.ClientID != clientID)
                return BadRequest();

            ViewBag.isDefaultGroup = groupId.Default;
            ViewBag.GroupName = groupId.Name;

            return View();
        }

        //GET /Group/UploadNumbers/{groupID}
        public IActionResult UploadNumbers(int? id)
        {
            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var groupId = db.Group.Find(id.Value);

            if (groupId == null || groupId.Default || groupId.ClientID != clientID)
                return BadRequest();

            ViewBag.GroupName = groupId.Name;
            ViewBag.ClientName = groupId.Client.Name;

            return View();
        }

        //POST /Group/CheckUploadedNumbers/{GroupID}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckUploadedNumbers(int? id, IFormFile file)
        {
            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var groupId = db.Group.Find(id.Value);

            if (groupId == null || groupId.Default || clientID != groupId.ClientID)
                return BadRequest();

            ViewBag.GroupName = groupId.Name;
            ViewBag.ClientName = groupId.Client.Name;

            // ClosedXML određuje format po ekstenziji, pa sačuvan fajl mora da ima .xlsx.
            string filename = id.ToString() + " " + DateTime.Now.ToString("yyyyMMddhhmmss") + ".xlsx";
            string pathToSave = UploadedFilesPath;
            Directory.CreateDirectory(pathToSave);

            if (file == null)
                return BadRequest("File not loaded");

            if (file.Length == 0)
                return BadRequest("Empty file");

            string filePath = Path.Combine(pathToSave, filename);

            // Stream se zatvara pre čitanja — inače je fajl još otvoren za upis.
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            DataTable dtData = GetDataFromExcel(filePath);

            var (model, badNumbers, existingNumbers, duplicates, badGroup, sendDeniedNumbers) =
                await CheckDataFromExcel((int)id, dtData);

            var clientId = db.Group.Where(g => g.GroupID == id).Select(g => g.ClientID).FirstOrDefault();
            int count = 1;

            db.Database.ExecuteSqlRaw("DELETE FROM BST_TEMP_IMPORT WHERE GroupId = @id", new SqlParameter("@id", id));

            db.ChangeTracker.AutoDetectChangesEnabled = false;

            foreach (var m in model.Numbers)
            {
                var tempNum = new TempImport()
                {
                    ClientId = clientId,
                    GroupId = (int)id,
                    Name = m.Name == "" ? null : m.Name,
                    Number = m.Number,
                    NumberType = m.NumberType == "VPN" ? NumberType.VPN : m.NumberType == "U MTS" ? NumberType.U_MTS : NumberType.VAN_MTS
                };

                db.BulkInsert(tempNum, count++, 100);
            }

            try
            {
                db.SaveChanges();
                logger.Info($"Number(s) successfully added to BST_TEMP_IMPORT GroupId = {id}");
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                ViewBag.Message = "!";
            }

            ViewBag.ExistingNumbers = existingNumbers.Length > 2 ? existingNumbers.Remove(existingNumbers.Length - 2) : "";
            ViewBag.BadNumbers = badNumbers.Length > 2 ? badNumbers.Remove(badNumbers.Length - 2) : "";
            ViewBag.BadGroup = badGroup.Length > 2 ? badGroup.Remove(badGroup.Length - 2) : "";
            ViewBag.Duplicates = duplicates.Length > 2 ? duplicates.Remove(duplicates.Length - 2) : "";
            ViewBag.SendDeniedNumbers = sendDeniedNumbers.Length > 2 ? sendDeniedNumbers.Remove(sendDeniedNumbers.Length - 2) : "";

            return View();
        }

        //POST /Group/ConfirmUploadNumbers/{GroupID}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ConfirmUploadNumbers(int? id)
        {
            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var group = db.Group.Find(id.Value);

            if (group == null || group.Default || group.ClientID != clientID)
                return BadRequest();

            try
            {
                db.Database.ExecuteSqlRaw("[dbo].[sp_insertDataFromTempImport] @GroupID", new SqlParameter("@GroupID", id));
            }
            catch (Exception ex)
            {
                logger.Error(ex.Message);
                ViewBag.Message = "!";
            }

            return RedirectToAction("Numbers", new { id = id });
        }

        //GET: /Group/AddNumber/{GroupID}
        public IActionResult AddNumber(int? id)
        {
            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var group = db.Group.Find(id.Value);

            if (group == null || group.Default || group.ClientID != clientID)
                return BadRequest();

            var numbersInGroup = db.GroupNumbers
                .Where(gn => gn.GroupID == id)
                .Select(gn => gn.NumberID).ToList();

            var selectListQry = db.GroupNumbers
                .Where(gn => gn.Groups.Default && gn.Numbers.ClientID == clientID)
                .Where(gn => !numbersInGroup.Contains(gn.NumberID) && gn.Numbers.SendAllowed && gn.Numbers.Active)
                .OrderBy(gn => gn.Numbers.Number)
                .Select(gn => new UserSelectListViewModel
                {
                    NumberID = gn.NumberID,
                    NameNumber = gn.Numbers.Number + " (" + gn.Numbers.Name + ")"
                }).ToList();

            UserAddNumberViewModel model = new UserAddNumberViewModel();
            model.Numbers = new MultiSelectList(selectListQry, "NumberID", "NameNumber");
            model.GroupID = (int)id;

            return View(model);
        }

        //POST: /Group/AddNumber/{NumberID}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddNumber(UserAddNumberViewModel model)
        {
            int clientID = CurrentClientId;
            var groupId = db.Group.Find(model.GroupID);

            if (model.NumberID == null || model.NumberID.Length == 0 || groupId == null || clientID != groupId.ClientID)
                return BadRequest();

            int count = 1;

            if (ModelState.IsValid)
            {
                for (int i = 0; i < model.NumberID.Length; i++)
                {
                    GroupNumberModel GroupNumber = new GroupNumberModel()
                    {
                        GroupID = model.GroupID,
                        NumberID = model.NumberID[i],
                        InsertDate = DateTime.Now
                    };

                    db.BulkInsert(GroupNumber, count++, 100);
                }

                db.SaveChanges();

                return RedirectToAction("Numbers", new { id = model.GroupID });
            }

            //TODO: initialize selectlist
            return View(model);
        }

        //GET: /Group/AddOneNumber/{GroupID}
        public IActionResult AddOneNumber(int? id)
        {
            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var groupId = db.Group.Find(id.Value);

            if (groupId == null || groupId.Default || clientID != groupId.ClientID)
                return BadRequest();

            UserAddOneNumberViewModel model = new UserAddOneNumberViewModel();
            model.GroupID = (int)id;
            ViewBag.GroupName = groupId.Name;
            return View(model);
        }

        //POST: /Group/AddOneNumber/{Number, NameNumber}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOneNumber(UserAddOneNumberViewModel model)
        {
            logger.SetControllerAction("Controllers/GroupController", "AddOneNumber");

            int clientID = CurrentClientId;
            var groupId = db.Group.Find(model.GroupID);

            if (groupId == null || groupId.Default || groupId.ClientID != clientID)
                return BadRequest();

            var numberFromUpload = model.Number;

            if (ModelState.IsValid)
            {
                string numberType = "";
                var activeNumberFromDb = db.Numbers.Where(n => n.Number == numberFromUpload && n.ClientID == clientID && n.Active);

                if (activeNumberFromDb.FirstOrDefault() == null)
                {
                    //provera broja
                    Match m = Regex.Match(numberFromUpload, @"^(06\d{7,8})$", RegexOptions.IgnoreCase);

                    if (m.Success)
                    {
                        // LDAP provera tipa broja (stub u Fazi 4, prava SOAP veza u Fazi 5)
                        numberType = await _ldap.GetNumberTypeAsync(numberFromUpload);

                        db.GroupNumbers.Add(new GroupNumberModel()
                        {
                            InsertDate = DateTime.Now,
                            GroupID = model.GroupID,
                            Numbers = new NumbersModel()
                            {
                                Active = true,
                                CheckDate = DateTime.Now,
                                ClientID = clientID,
                                InsertDate = DateTime.Now,
                                Name = model.NameNumber,
                                Number = model.Number,
                                NumberTypeID = db.NumberType.Where(nt => nt.Name == numberType).FirstOrDefault().NumberTypeID,
                                SendAllowed = true
                            }
                        });
                    }
                    else
                    {
                        ViewBag.BadNumbers = numberFromUpload;
                        return View(model);
                    }
                }
                else
                {
                    var existingActiveSendAllowedVpnNumberInDb = activeNumberFromDb.Where(n => n.NumberTypeID == 1 && n.SendAllowed).FirstOrDefault();

                    var numberToAddToGroup = new NumbersModel();

                    //ako u bazi postoji taj VPN broj koji je aktivan i sendAllowed, dodaj ga u grupu
                    if (existingActiveSendAllowedVpnNumberInDb != null)
                    {
                        numberToAddToGroup = existingActiveSendAllowedVpnNumberInDb;
                        goto AddNumberToGroup;
                    }

                    var existingActiveSendAllowedNonVpnNumberInDb = activeNumberFromDb.Where(n => n.NumberTypeID != 1 && n.SendAllowed).FirstOrDefault();

                    //ako je NON VPN koji je aktivan i sendAllowed, dodaj ga u grupu, u suprotnom moze jedino da bude sendDenied i tada izbaci obavestenje da je zabranjeno slanje na ovaj broj
                    if (existingActiveSendAllowedNonVpnNumberInDb != null)
                    {
                        numberToAddToGroup = existingActiveSendAllowedNonVpnNumberInDb;
                        goto AddNumberToGroup;
                    }
                    else
                    {
                        ViewBag.SendDeniedNumber = numberFromUpload;
                        return View(model);
                    }

                AddNumberToGroup:
                    db.GroupNumbers.Add(new GroupNumberModel()
                    {
                        GroupID = model.GroupID,
                        NumberID = numberToAddToGroup.NumberID,
                        InsertDate = DateTime.Now
                    });
                }

                try
                {
                    db.SaveChanges();
                    logger.Info($"Number {numberFromUpload} successfully added to group {model.GroupID}");
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                    ViewBag.Message = "!";
                    return View(model);
                }

                return RedirectToAction("Numbers", new { id = model.GroupID });
            }

            return View(model);
        }

        //GET: /Group/EditNumber/{GroupID}/{NumberID}
        public IActionResult EditNumber(int? groupId, int? numberId)
        {
            if (numberId == null || groupId == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var number = db.Numbers.Find(numberId.Value);

            if (number == null || number.ClientID != clientID)
                return BadRequest();

            UserEditNumberViewModel model = new UserEditNumberViewModel()
            {
                GroupID = (int)groupId,
                NumberID = number.NumberID,
                Name = number.Name,
                Number = number.Number
            };

            return View(model);
        }

        //POST: /Group/EditNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditNumber(UserEditNumberViewModel model)
        {
            if (ModelState.IsValid)
            {
                var number = db.Numbers.Find(model.NumberID);
                int clientID = CurrentClientId;

                if (number == null || number.ClientID != clientID)
                    return BadRequest();

                number.Name = model.Name;
                number.InsertDate = DateTime.Now;
                db.SaveChanges();

                return RedirectToAction("Numbers", routeValues: new { id = model.GroupID });
            }

            return View(model);
        }

        //GET: /Group/ListNumbers
        public IActionResult ListNumbers()
        {
            //segment ispod zakomentarisan jer se sa forme poziva metoda sa API -> GetListNumbers koja puni tabelu
            return View();
        }

        //GET: /Group/EditListNumber/{NumberID}
        public IActionResult EditListNumber(int? id)
        {
            if (id == null)
                return BadRequest();

            int clientID = CurrentClientId;
            var number = db.Numbers.Find(id.Value);

            if (number == null || number.ClientID != clientID)
                return BadRequest();

            UserEditNumberViewModel model = new UserEditNumberViewModel()
            {
                NumberID = number.NumberID,
                Name = number.Name,
                Number = number.Number
            };

            return View(model);
        }

        //POST: /Group/EditListNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditListNumber(UserEditNumberViewModel model)
        {
            if (ModelState.IsValid)
            {
                var number = db.Numbers.Find(model.NumberID);
                int clientID = CurrentClientId;

                if (number == null || number.ClientID != clientID)
                    return BadRequest();

                number.Name = model.Name;
                db.SaveChanges();

                return RedirectToAction("ListNumbers");
            }

            return View(model);
        }

        #region FileDowload
        public IActionResult DownloadTemplate()
        {
            return PhysicalFile(Path.Combine(UploadedFilesPath, "UploadNumbersTemplate.xlsx"),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "UploadNumbersTemplate.xlsx");
        }

        //TODO: Zakaciti uputstvo za klijente
        public IActionResult DownloadManual()
        {
            return PhysicalFile(Path.Combine(UploadedFilesPath, "AdminManual.pdf"), "application/pdf", "AdminManual.pdf");
        }
        #endregion

        private DataTable GetDataFromExcel(string filePath)
        {
            var dt = new DataTable();
            dt.Columns.Add("Number");
            dt.Columns.Add("Name");

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheets.First();

            // Ekvivalent EPPlus worksheet.Dimension; prazan list → nema redova.
            var range = worksheet.RangeUsed();
            if (range == null)
                return dt;

            int startRow = range.FirstRow().RowNumber();
            int endRow = range.LastRow().RowNumber();
            int startCol = range.FirstColumn().ColumnNumber();
            int endCol = range.LastColumn().ColumnNumber();

            for (int row = startRow + 1; row <= endRow; row++)
            {
                DataRow dr = dt.NewRow();
                int i = 0;

                for (int col = startCol; col <= endCol; col++)
                {
                    // Kao EPPlus Value.ToString(): broj upisan kao broj gubi vodeću nulu (isto kao legacy).
                    dr[i++] = worksheet.Cell(row, col).Value.ToString();
                }
                dt.Rows.Add(dr);
            }

            return dt;
        }

        private async Task<(ClientConfirmUploadFileViewModel model, string badNumbers, string existingNumbers,
            string duplicates, string badGroup, string sendDeniedNumbers)> CheckDataFromExcel(int id, DataTable dtData)
        {
            ClientConfirmUploadFileViewModel model = new ClientConfirmUploadFileViewModel();
            model.Numbers = new List<ClientUploadFileViewModel>();

            string badNumbers = "";
            string existingNumbers = "";
            string duplicates = "";
            string badGroup = "";
            string sendDeniedNumbers = "";

            int clientID = CurrentClientId;
            var group = db.Group.Find(id);

            if (group == null || group.Default || clientID != group.ClientID)
            {
                badGroup = group?.Name ?? "";
                return (model, badNumbers, existingNumbers, duplicates, badGroup, sendDeniedNumbers);
            }

            var rows = dtData.Rows.Cast<DataRow>().ToList();

            //uzimanje jedinstvenih brojeva iz excel fajla
            var uniqueNumbers = rows
                .GroupBy(x => x["Number"]?.ToString())
                .Select(g => g.First());

            var listOfDuplicates = rows.GroupBy(r => r[0]?.ToString()).Where(gr => gr.Count() > 1).ToList();
            foreach (var number in listOfDuplicates)
            {
                duplicates += number.Key + ", ";
            }

            string numberType;

            foreach (var row in uniqueNumbers)
            {
                string numberFromFile = (row["Number"]?.ToString() ?? "").Trim();
                string nameFromFile = (row["Name"]?.ToString() ?? "").Trim();

                numberType = "";
                var activeNumberFromDb = db.Numbers.Where(n => n.Number == numberFromFile && n.ClientID == clientID && n.Active);

                if (activeNumberFromDb.FirstOrDefault() == null)
                {
                    //provera broja
                    Match m = Regex.Match(numberFromFile, @"^(06\d{7,8})$", RegexOptions.IgnoreCase);

                    if (m.Success)
                    {
                        // LDAP provera tipa broja (stub u Fazi 4, prava SOAP veza u Fazi 5)
                        numberType = await _ldap.GetNumberTypeAsync(numberFromFile);

                        //radi se upis broja
                        model.Numbers.Add(new ClientUploadFileViewModel()
                        {
                            Name = nameFromFile,
                            Number = numberFromFile,
                            NumberType = numberType
                        });
                    }
                    else
                    {
                        badNumbers += numberFromFile + ", ";
                    }
                }
                else
                {
                    //ovde moze da prodje aktivni VPN ili aktivni non VPN.
                    //Provera da li je broj u grupi treba da se uradi za aktivne brojeve koji su sendAllowed. Za broj koji je u grupi ali je sendDenied, dobice obavestenje
                    var activeSendAllowedNumberInGroup = db.GroupNumbers.Where(gn => gn.GroupID == id)
                        .Join(db.Numbers.Where(n => n.Number == numberFromFile && n.Active && n.SendAllowed),
                            gnums => gnums.NumberID,
                            nums => nums.NumberID,
                            (gnums, nums) => new { GroupNums = gnums, Numbers = nums }).FirstOrDefault();

                    if (activeSendAllowedNumberInGroup == null)
                    {
                        var existingActiveSendAllowedVpnNumberInDb = activeNumberFromDb.Where(n => n.NumberTypeID == 1 && n.SendAllowed).FirstOrDefault();

                        var numberToAddToGroup = new NumbersModel();

                        //ako u bazi postoji taj VPN broj koji je aktivan i sendAllowed, dodaj ga u grupu
                        if (existingActiveSendAllowedVpnNumberInDb != null)
                        {
                            numberToAddToGroup = existingActiveSendAllowedVpnNumberInDb;
                            goto AddNumberToGroup;
                        }

                        var existingActiveSendAllowedNonVpnNumberInDb = activeNumberFromDb.Where(n => n.NumberTypeID != 1 && n.SendAllowed).FirstOrDefault();

                        //ako je NON VPN koji je aktivan i sendAllowed, dodaj ga u grupu, u suprotnom moze jedino da bude sendDenied i tada izbaci obavestenje da je zabranjeno slanje na ovaj broj
                        if (existingActiveSendAllowedNonVpnNumberInDb != null)
                        {
                            numberToAddToGroup = existingActiveSendAllowedNonVpnNumberInDb;
                            goto AddNumberToGroup;
                        }
                        else
                        {
                            sendDeniedNumbers += numberFromFile + ", ";
                            continue;
                        }

                    AddNumberToGroup: //u bazi procedura za upis brojeva iz temp import tabele proverava da li su vec prisutni (aktivni) brojevi koji se upisuju u bazu, i ako ih ima onda ih samo doda u grupu (ako su aktivni i sendAllowed) bez upisivanja u numbers tabelu
                        model.Numbers.Add(new ClientUploadFileViewModel()
                        {
                            Name = numberToAddToGroup.Name,
                            Number = numberToAddToGroup.Number,
                            NumberType = numberToAddToGroup.NumberType.Name.ToString()
                        });
                    }
                    else
                    {
                        existingNumbers += numberFromFile + ", ";
                    }
                }
            }

            return (model, badNumbers, existingNumbers, duplicates, badGroup, sendDeniedNumbers);
        }
    }
}
