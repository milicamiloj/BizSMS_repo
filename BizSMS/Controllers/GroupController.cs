using BizSMS.Attributes;
using BizSMS.Helpers;
using BizSMS.Models;
using Microsoft.AspNet.Identity;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;

namespace BizSMS.Controllers
{
    [AuthorizeUser(Roles = "User,Client")]
    public class GroupController : BaseController
    {
        Logger logger = new Logger();
        // GET: Group
        public ActionResult Index()
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

            var modelQry = (from groups in db.Group
                            join group_numbers in db.GroupNumbers on groups.GroupID equals group_numbers.GroupID into group_number
                            where groups.ClientID == clientID
                            from gn in group_number.DefaultIfEmpty()
                            group gn by groups.GroupID into grouped
                            select new
                            {
                                GroupID = grouped.Key,
                                TotalOfNumbers = grouped.Count(t => t.NumberID != null && t.Numbers.SendAllowed && t.Numbers.Active)
                            }).ToList();

            List<UserManageGroupsViewModel> model = new List<UserManageGroupsViewModel>();

            foreach(var m in modelQry)
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
        public ActionResult CreateGroup()
        {
            return View();
        }

        //POST: /Group/EditGroup/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateGroup(UserCreateGroupViewModel model)
        {
            if (ModelState.IsValid)
            {
                int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

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
        public ActionResult EditGroup(int? id)
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var group = db.Group.Find(id);

            if(id == null || group == null || group.ClientID != clientID || group.Default)
            {
                throw new HttpException(400, "Bad Request");
            }

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
        public ActionResult EditGroup(UserEditGroupViewModel model)
        {
            if(ModelState.IsValid)
            {
                var group = db.Group.Find(model.GroupID);
                int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

                if (group == null || group.ClientID != clientID)
                {
                    throw new HttpException(403, "Unauthorized");
                }

                group.Name = model.Name;
                group.InsertDate = DateTime.Now;
                db.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(model);
        }

        //GET: /Group/Numbers/{GroupID}
        public ActionResult Numbers(int? id)
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var groupId = db.Group.Find(id);

            if (id == null || groupId == null || groupId.ClientID != clientID)
            {
                throw new HttpException(400, "Bad Request");
            }

            ViewBag.isDefaultGroup = groupId.Default;
            ViewBag.GroupName = groupId.Name;

            return View();
        }

        //GET /Group/UploadNumbers/{groupID}
        public ActionResult UploadNumbers(int? id)
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var groupId = db.Group.Find(id);

            if (id == null || groupId.Default == true || groupId.ClientID != clientID)
            {
                throw new HttpException(400, "Bad Request");
            }

            ViewBag.GroupName = groupId.Name;
            ViewBag.ClientName = groupId.Client.Name;

            return View();
        }

        //POST /Group/CheckUploadedNumbers/{GroupID}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckUploadedNumbers(int? id, HttpPostedFileBase file)
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var groupId = db.Group.Find(id);

            if (groupId == null || id == null || groupId.Default == true || clientID != groupId.ClientID)
            {
                throw new HttpException(400, "Bad Request");
            }

            ViewBag.GroupName = groupId.Name;
            ViewBag.ClientName = groupId.Client.Name;

            string filename = id.ToString() + " " + DateTime.Now.ToString("yyyyMMddhhmmss");
            string pathToSave = Server.MapPath("~/UploadedFiles");

            if (file == null)
            {
                throw new HttpException(400, "File not loaded");
            }
            else if (file.ContentLength > 0)
            {
                file.SaveAs(System.IO.Path.Combine(pathToSave, filename));
            }
            else
            {
                throw new HttpException(400, "Empty file");
            }

            string filePath = Server.MapPath("~/UploadedFiles/" + filename);

            DataTable dtData = GetDataFromExcel(filePath);
            string badNumbers = "";
            string existingNumbers = "";
            string sendDeniedNumbers = "";
            string duplicates = "";
            string badGroup = "";

            ClientConfirmUploadFileViewModel model = CheckDataFromExcel((int)id, dtData, out badNumbers, out existingNumbers, out duplicates, out badGroup, out sendDeniedNumbers);

            var clientId = db.Group.Where(g => g.GroupID == id).Select(g => g.ClientID).FirstOrDefault();
            int count = 1;

            db.Database.ExecuteSqlCommand($"DELETE FROM BST_TEMP_IMPORT WHERE GroupId = @id", new SqlParameter("@id", id)); 

            db.Configuration.AutoDetectChangesEnabled = false;

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
        public ActionResult ConfirmUploadNumbers(int? id)
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var group = db.Group.Find(id);

            if (group == null || id == null || group.Default == true || group.ClientID != clientID)
            {
                throw new HttpException(400, "Bad Request");
            }

            if (id != null)
            {
                try
                {
                    db.Database.ExecuteSqlCommand("[dbo].[sp_insertDataFromTempImport] @GroupID", new SqlParameter("@GroupID", id));
                }
                catch (Exception ex)
                {
                    logger.Error(ex.Message);
                    ViewBag.Message = "!";
                }
            }

            return RedirectToAction("Numbers", new { id = id });
        }

        //GET: /Group/AddNumber/{GroupID}
        public ActionResult AddNumber(int? id)
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var group = db.Group.Find(id);

            if (group == null || id == null || group.Default == true || group.ClientID != clientID)
            {
                throw new HttpException(400, "Bad Request");
            }

            var numbersInGroup = db.GroupNumbers
            .Where(gn => gn.GroupID == id)
            .Select(gn => gn.NumberID).ToList();

            var selectListQry = db.GroupNumbers
                .Where(gn => gn.Groups.Default == true && gn.Numbers.ClientID == clientID)
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
        public ActionResult AddNumber(UserAddNumberViewModel model)
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var groupId = db.Group.Find(model.GroupID);

            if (model.NumberID == null || model.NumberID.Length == 0 || clientID != groupId.ClientID)
            {
                throw new HttpException(400, "Bad Request");
            }

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

                    //db.GroupNumbers.Add(GroupNumber);
                    db.BulkInsert(GroupNumber, count++, 100);
                }

                db.SaveChanges();

                return RedirectToAction("Numbers", new { id = model.GroupID });
            }

            //TODO: initialize selectlist
            return View(model);
        }

        //GET: /Group/AddOneNumber/{GroupID}
        public ActionResult AddOneNumber(int? id)
        {
            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var groupId = db.Group.Find(id);

            if (groupId == null || id == null || groupId.Default == true || clientID != groupId.ClientID)
            {
                throw new HttpException(400, "Bad Request");
            }

            UserAddOneNumberViewModel model = new UserAddOneNumberViewModel();
            model.GroupID = (int)id;
            ViewBag.GroupName = groupId.Name;
            return View(model);

        }

        //POST: /Group/AddOneNumber/{Number, NameNumber}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddOneNumber (UserAddOneNumberViewModel model)
        {
            logger.SetControllerAction("Controllers/GroupController", "AddOneNumber");

            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var groupId = db.Group.Find(model.GroupID);

            if (groupId == null || groupId.Default == true || groupId.ClientID != clientID)
            {
                throw new HttpException(400, "Bad Request");
            }

            var numberFromUpload = model.Number;
            var nameFromUpload = model.NameNumber;

            //ldapCheckWS.LDAPCheck ldap = new ldapCheckWS.LDAPCheck();
            SelfCareLdapWS.LDAPCheckSoapClient ldap = new SelfCareLdapWS.LDAPCheckSoapClient();

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
                        #region searchLDAP and get numberType

                        using (new OperationContextScope(ldap.InnerChannel))
                        {
                            var httpRequestProperty = new HttpRequestMessageProperty();
                            httpRequestProperty.Headers.Add("Integration-Auth", ConfigurationManager.AppSettings["integrationAuth.bizsms"]);
                            //httpRequestProperty.Headers.Add("Integration-Auth", "Yml6c21zOkIheiRtUzAu");
                            OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = httpRequestProperty;

                            SelfCareLdapWS.vratiTipKorisnikaRequestBody req = new SelfCareLdapWS.vratiTipKorisnikaRequestBody();
                            req.msisdn = "381" + numberFromUpload.Remove(0, 1).Trim();
                            var result = ldap.vratiTipKorisnika(req.msisdn);

                            if (result != null)
                            {
                                numberType = "U MTS";
                            }
                            else
                            {
                                numberType = "VAN MTS";
                            }
                        }

                        //if (ldap.vratiTipKorisnika("381" + numberFromUpload.Remove(0, 1).Trim()) != null)
                        //{
                        //    //broj postoji u ldap-u -> znaci da je u MTS-u; 
                        //    numberType = "U MTS";
                        //}
                        //else
                        //{
                        //    //broj ne postoji u ldap-u -> znaci da nije u MTS-u;    
                        //    numberType = "VAN MTS";
                        //}
                        #endregion

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
                catch(Exception ex)
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
        public ActionResult EditNumber(int? groupId, int? numberId)
        {
            if (numberId == null || groupId == null)
            {
                throw new HttpException(400, "Bad Request");
            }

            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var number = db.Numbers.Find(numberId);

            if (number == null || number.ClientID != clientID)
            {
                throw new HttpException(400, "Bad Request");
            }

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
        public ActionResult EditNumber(UserEditNumberViewModel model)
        {
            if (ModelState.IsValid)
            {
                var number = db.Numbers.Find(model.NumberID);
                int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

                if (number == null || number.ClientID != clientID)
                {
                    throw new HttpException(400, "Bad Request");
                }

                number.Name = model.Name;
                number.InsertDate = DateTime.Now;
                db.SaveChanges();
                
                return RedirectToAction("Numbers", routeValues: new { id = model.GroupID });
            }

            return View(model);
        }

        //GET: /Group/ListNumbers
        public ActionResult ListNumbers()
        {
            //segment ispod zakomentarisan jer sa sa forme poziva metoda sa API -> GetListNumbers koja puni tabelu
            //int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            //List<UserManageNumbersViewModel> model = db.Numbers
            //    .Where(n => n.ClientID == clientID
            //                && n.Active && n.SendAllowed)
            //    .Select(n => new UserManageNumbersViewModel
            //    {
            //        NumberID = n.NumberID,
            //        Number = n.Number,
            //        Name = n.Name,
            //        NumberType = n.NumberType.Name
            //    })
            //    .ToList();

            return View();
        }

        //GET: /Group/EditListNumber/{NumberID}
        public ActionResult EditListNumber(int? id)
        {
            if (id == null)
            {
                throw new HttpException(400, "Bad Request");
            }

            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var number = db.Numbers.Find(id);

            if (number == null || number.ClientID != clientID)
            {
                throw new HttpException(400, "Bad Request");
            }

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
        public ActionResult EditListNumber(UserEditNumberViewModel model)
        {
            if (ModelState.IsValid)
            {
                var number = db.Numbers.Find(model.NumberID);
                int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;

                if (number == null || number.ClientID != clientID)
                {
                    throw new HttpException(400, "Bad Request");
                }

                number.Name = model.Name;
                db.SaveChanges();

                return RedirectToAction("ListNumbers");
            }

            return View(model);
        }

        #region FileDowload
        public FileResult DownloadTemplate()
        {
            return File("~/UploadedFiles/UploadNumbersTemplate.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }

        //TODO: Zakaciti uputstvo za klijente
        public FileResult DownloadManual()
        {
            return File("~/UploadedFiles/AdminManual.pdf", "application/pdf");
        }

        #endregion

        private DataTable GetDataFromExcel(string filePath)
        {
            var dt = new DataTable();
            var existingFile = new FileInfo(filePath);

            using (ExcelPackage xlPackage = new ExcelPackage(existingFile))
            {
                //Get the worksheet in the workbook 
                ExcelWorksheet worksheet = xlPackage.Workbook.Worksheets.First();

                //Obtain the worksheet size 
                ExcelCellAddress startCell = worksheet.Dimension.Start;
                ExcelCellAddress endCell = worksheet.Dimension.End;

                //Create the data column 
                dt.Columns.Add("Number");
                dt.Columns.Add("Name");


                for (int row = startCell.Row + 1; row <= endCell.Row; row++)
                {
                    DataRow dr = dt.NewRow(); //Create a row
                    int i = 0;

                    for (int col = startCell.Column; col <= endCell.Column; col++)
                    {
                        var cellValue = worksheet.Cells[row, col].Value;
                        dr[i++] = cellValue == null ? "" : cellValue.ToString();
                    }
                    dt.Rows.Add(dr);

                }
            }

            return dt;
        }

        private ClientConfirmUploadFileViewModel CheckDataFromExcel(int id, DataTable dtData, out string badNumbers, out string existingNumbers, out string duplicates, out string badGroup, out string sendDeniedNumbers)
        {
            ClientConfirmUploadFileViewModel model = new ClientConfirmUploadFileViewModel();
            model.Numbers = new List<ClientUploadFileViewModel>();

            badNumbers = "";
            existingNumbers = "";
            duplicates = "";
            badGroup = "";
            sendDeniedNumbers = "";

            int clientID = UserManager.FindById(User.Identity.GetUserId()).ClientID;
            var group = db.Group.Find(id);

            if (group == null || group.Default == true || clientID != group.ClientID)
            {
                badGroup = group.Name;
                return model;
            }

            //uzimanje jedinstvenih brojeva iz excel fajla
            var uniqueNumbers = dtData.AsEnumerable()
                       .GroupBy(x => x.Field<string>("Number"))
                       .Select(g => g.First());
            
            var listOfDuplicates = dtData.AsEnumerable().GroupBy(r => r[0]).Where(gr => gr.Count() > 1).ToList();
            foreach (var number in listOfDuplicates)
            {
                duplicates += number.Key + ", ";
            }

            string numberType;

            //LDAPSearcher ldap = new LDAPSearcher();
            //ldapCheckWS.LDAPCheck ldap = new ldapCheckWS.LDAPCheck();
            SelfCareLdapWS.LDAPCheckSoapClient ldap = new SelfCareLdapWS.LDAPCheckSoapClient();

            foreach (var row in uniqueNumbers)
            {
                string numberFromFile = row.Field<string>("Number").Trim();
                string nameFromFile = row.Field<string>("Name").Trim();

                numberType = "";
                var activeNumberFromDb = db.Numbers.Where(n => n.Number == numberFromFile && n.ClientID == clientID && n.Active);
                
                if (activeNumberFromDb.FirstOrDefault() == null)
                {
                    //provera broja
                    Match m = Regex.Match(numberFromFile, @"^(06\d{7,8})$", RegexOptions.IgnoreCase);

                    if (m.Success)
                    {
                        #region searchLDAP and get numberType

                        using (new OperationContextScope(ldap.InnerChannel))
                        {
                            var httpRequestProperty = new HttpRequestMessageProperty();
                            httpRequestProperty.Headers.Add("Integration-Auth", ConfigurationManager.AppSettings["integrationAuth.bizsms"]);
                            //httpRequestProperty.Headers.Add("Integration-Auth", "Yml6c21zOkIheiRtUzAu");
                            OperationContext.Current.OutgoingMessageProperties[HttpRequestMessageProperty.Name] = httpRequestProperty;

                            SelfCareLdapWS.vratiTipKorisnikaRequestBody req = new SelfCareLdapWS.vratiTipKorisnikaRequestBody();
                            req.msisdn = "381" + numberFromFile.Remove(0, 1).Trim();
                            var result = ldap.vratiTipKorisnika(req.msisdn);

                            if (result != null)
                            {
                                numberType = "U MTS";
                            }
                            else
                            {
                                numberType = "VAN MTS";
                            }
                        }
                        //if (ldap.vratiTipKorisnika("381" + numberFromFile.Remove(0, 1).Trim()) != null)
                        //{
                        //    //broj postoji u ldap-u -> znaci da je u MTS-u; 
                        //    numberType = "U MTS";
                        //}
                        //else
                        //{
                        //    //broj ne postoji u ldap-u -> znaci da nije u MTS-u;    
                        //    numberType = "VAN MTS";
                        //}
                        #endregion

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
            return model;
        }
    }
}