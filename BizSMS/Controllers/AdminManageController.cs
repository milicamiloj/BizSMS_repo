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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace BizSMS.Controllers
{
    [AuthorizeUser(Roles = "Administrator")]
    public class AdminManageController : BaseController
    {
        readonly Logger logger = new Logger();

        #region Clients
        // GET: /AdminManage/
        public ActionResult Index()
        {
            return RedirectToAction("ManageClients");
        }

        /// <summary>
        /// if clientID is present, toggling IsCanceled field
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        // GET: /AdminManage/ManageClients/{ClientID}
        public ActionResult ManageClients(int? id)
        {
            logger.SetControllerAction("AdminManageController", "ManageClients");
            if (id != null)
            {
                logger.Info("Lock client with id: " + id.ToString());
                var client = db.Client.Find(id);
                client.IsCanceled = !client.IsCanceled;
                var users = db.Users.Where(u => u.ClientID == id && u.IsDeleted == false).ToList();

                foreach(var user in users)
                {
                    user.IsCanceled = !user.IsCanceled;
                }

                db.SaveChanges();
            }

            var clients = db.Client.Where(c => c.Name != "Telekom").ToList();
            List<AdminManageClientsViewModel> model = new List<AdminManageClientsViewModel>();

            logger.Info("List clients");

            foreach (var client in clients)
            {
                model.Add(new AdminManageClientsViewModel
                {
                    ClientID = client.ClientID,
                    Name = client.Name,
                    MtsID = client.MtsID,
                    PhoneNumber = client.PhoneNumber,
                    IsCanceled = client.IsCanceled,
                    Username = string.Join(", ", client.ApplicationUsers.Where(au => au.IsDeleted == false).Select(u => u.UserName).ToList())
                });
            }

            return View(model);
        }

        // GET: /AdminManage/CreateClient
        public ActionResult CreateClient(string message)
        {
            ViewBag.StatusMessage = message;
            return View();
        }


        //POST: AdminManage/CreateClient
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateClient(AdminCreateClientViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "CreateClient");
            if (ModelState.IsValid)
            {
                var client = db.Client.FirstOrDefault(c => c.Name == model.ClientName);

                if (client == null)
                {
                    logger.Info("Create new client with contractID: " + model.ContractID);
                    var contracts = new List<ClientContractsModel>();
                    contracts.Add(new ClientContractsModel() { ContractId = model.ContractID, SynchronizationDate = new DateTime(1900, 1, 1) });

                    ClientModel Client = new ClientModel()
                    {
                        MtsID = model.Mts_ID,
                        Name = model.ClientName,
                        PhoneNumber = model.PhoneNumber,
                        IsCanceled = false,
                        Contracts = contracts,
                        InsertDate = DateTime.Now
                    };

                    db.Client.Add(Client);
                    logger.Info("Create new user for client: " + model.ClientName);
                    ApplicationUser user = new ApplicationUser()
                    {
                        UserName = model.Username,
                        //Email = model.Email,
                        ClientID = Client.ClientID,
                        PhoneNumber = model.PhoneNumber
                    };

                    var result = await UserManager.CreateAsync(user, model.Password);
                    logger.Info("User created with username: " + user.UserName);
                    var lockout = await UserManager.SetLockoutEnabledAsync(user.Id, true);
                    var roles = await UserManager.AddToRoleAsync(user.Id, "Client");
                    logger.Info("Client role added to user");

                    GroupModel VPNGroup = new GroupModel()
                    {
                        ClientID = Client.ClientID,
                        Name = "VPN",
                        Default = true,
                        InsertDate = DateTime.Now
                    };

                    //GroupModel NoVPNGroup = new GroupModel()
                    //{
                    //    ClientID = Client.ClientID,
                    //    Name = "VAN VPN",
                    //    Default = true
                    //};

                    db.Group.Add(VPNGroup);
                    //db.Group.Add(NoVPNGroup);

                    AlphanumericModel Alphanumeric = new AlphanumericModel()
                    {
                        Alphanumeric = model.PhoneNumber,
                        ClientID = Client.ClientID,
                        InsertDate = DateTime.Now
                    };

                    db.Alphanumeric.Add(Alphanumeric);

                    db.SaveChanges();
                    logger.Info("Default VPN group AND alphanumeric " + model.PhoneNumber + " created");
                    //prevuci brojeve iz TIS u BizSMS bazu za zadati Contract_ID odmah po kreiranju klijenta
                    try
                    {
                        logger.Info("Load VPN numbers");
                        db.Database.ExecuteSqlCommand("EXEC dbo.sp_InsertNumbers {0}", model.ContractID);
                        logger.Info("VPN numbers loaded successfully");
                    }
                    catch (SqlException ex)
                    {
                        logger.Error(ex.Message);
                    }

                    return RedirectToAction("Alphanumerics", new { id = Client.ClientID });
                }
            }
            return View(model);
        }

        //GET: AdminManage/EditClient/{ClientID}
        public async Task<ActionResult> EditClient(int? id)
        {
            logger.SetControllerAction("AdminManageController", "EditClient");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad Request");
            }

            ClientModel client = db.Client.Find(id);

            if (client == null)
            {
                logger.Warn("Client not found with id: " + id.ToString());
                throw new HttpException(400, "Bad Request");
            }

            //ne postoji ClientUser tako da nema ni potrebe za ovim selectedUser
            //ApplicationUser selectedUser = await GetClientUser(client);

            AdminEditClientViewModel model = new AdminEditClientViewModel()
            {
                ClientID = client.ClientID,
                Mts_ID = client.MtsID,
                ClientName = client.Name,
                ContractID = string.Join(", ", client.Contracts.Where(cc => cc.ClientId == id).Select(c => c.ContractId).ToList()),
                Username = string.Join(", ", client.ApplicationUsers.Where(au => au.IsDeleted == false).Select(u => u.UserName).ToList()),
                PhoneNumber = client.PhoneNumber
            };
            logger.Info("Showing client data");
            return View(model);
        }

        //POST: AdminManage/EditClient/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditClient(AdminEditClientViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "EditClient");
            if (ModelState.IsValid)
            {
                ClientModel client = db.Client.Find(model.ClientID);
                logger.Info("Change client data from: mts_id - " + client.MtsID + " clientName - " + client.Name + " phone - " + client.PhoneNumber);
                client.MtsID = model.Mts_ID;
                client.Name = model.ClientName;
                client.PhoneNumber = model.PhoneNumber;

                //ne postoji ClientUser tako da nema ni potrebe za ovim user.PhoneNumber
                //ApplicationUser user = await GetClientUser(client);
                //user.PhoneNumber = model.PhoneNumber;

                //UserManager.Update(user);

                db.SaveChanges();
                logger.Info("Client data changed to: mts_id - " + model.Mts_ID + " clientName - " + model.ClientName + " phone - " + model.PhoneNumber);

                return RedirectToAction("ManageClients");
            }
            logger.Warn("Invalid form data");

            return View(model);
        }

        //GET: AdminManage/ClientContracts/{ClientID}
        public async Task<ActionResult> ClientContracts(int? id)
        {
            logger.SetControllerAction("AdminManageController", "ClientContracts");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad Request");
            }

            var client = await db.Client.FindAsync(id);

            if (client == null)
            {
                logger.Warn("Client not found with id: " + id.ToString());
                throw new HttpException(400, "Bad Request");
            }

            ClientContractsViewModel model = new ClientContractsViewModel()
            {
                ClientId = client.ClientID,
                ClientName = client.Name
            };
            logger.Info("Show client contracts");

            return View(model);
        }

        public async Task<ActionResult> AddClientContract(int? id)
        {
            logger.SetControllerAction("AdminManageController", "AddClientContract");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad Request");
            }
            logger.Info("Get Client for clientId: " + id.ToString());
            var client = await db.Client.FindAsync(id);

            if (client == null)
            {
                logger.Warn("Client not found with id: " + id.ToString());
                throw new HttpException(400, "Bad Request");
            }

            AddClientContractViewModel model = new AddClientContractViewModel()
            {
                ClientId = client.ClientID,
                ClientName = client.Name
            };

            logger.Info("Show form for add contract");

            return View(model);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> AddClientContract(AddClientContractViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "AddClientContract");
            if (!ModelState.IsValid)
            {
                logger.Warn("Invalid form data");
                return View(model);
            }

            var client = await db.Client.FindAsync(model.ClientId);

            client.Contracts = new List<ClientContractsModel>();
            client.Contracts.Add(new ClientContractsModel()
            {
                //ClientId = model.ClientId,
                ContractId = model.ContractId,
                SynchronizationDate = new DateTime(1900, 1, 1)
            });

            db.SaveChanges();
            logger.Warn("Contract " + model.ContractId + " successfully added");
            //prevuci brojeve iz TIS u BizSMS bazu za zadati Contract_ID odmah po kreiranju novog ugovora za vec postojeceg klijenta (zato sto je klijent vec postojeci poziva se procedura RefreshNumbers umesto InsertNumbers)
            try
            {
                logger.Info("Load numbers for new contract");
                db.Database.ExecuteSqlCommand("EXEC dbo.sp_RefreshNumbers {0}", model.ContractId);
                logger.Info("Numbers successfully loaded");
            }
            catch (SqlException ex)
            {
                logger.Error(ex.ToString());
            }

            return RedirectToAction("ClientContracts", "AdminManage", new { id = client.ClientID });
        }

        public async Task<ActionResult> EditClientContract(int? id)
        {
            logger.SetControllerAction("AdminManageController", "EditClientContract");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad Request");
            }

            logger.Info("Get contract for contractId: " + id.ToString());
            var contract = await db.ClientContract.FindAsync(id);

            if (contract == null)
            {
                logger.Warn("Contract not found");
                throw new HttpException(400, "Bad Request");
            }

            EditClientContractViewModel model = new EditClientContractViewModel()
            {
                ClientContractId = contract.ClientContractsId,
                ClientId = contract.ClientId,
                ClientName = contract.Client.Name,
                ContractId = contract.ContractId
            };

            logger.Info("Show contract");

            return View(model);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<ActionResult> EditClientContract(EditClientContractViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "EditClientContract");
            if (!ModelState.IsValid)
            {
                logger.Warn("Invalid form data");
                return View(model);
            }
            logger.Info("Get contract for contractId: " + model.ClientContractId);
            var contract = await db.ClientContract.FindAsync(model.ClientContractId);

            contract.ContractId = model.ContractId;

            db.SaveChanges();
            logger.Info("Contract successfully updated");
            //prevuci brojeve iz TIS u BizSMS bazu za zadati Contract_ID odmah po izmeni ContractId za vec postojeceg klijenta (zato sto je klijent vec postojeci poziva se procedura RefreshNumbers umesto InsertNumbers)
            try
            {
                logger.Info("Load numbers for new contract");
                db.Database.ExecuteSqlCommand("EXEC dbo.sp_RefreshNumbers {0}", model.ContractId);
                logger.Info("Numbers successfully loaded");
            }
            catch (SqlException ex)
            {
                logger.Error(ex.ToString());
            }

            return RedirectToAction("ClientContracts", "AdminManage", new { id = contract.ClientId });
        }
        #endregion

        #region ClientUsers

        //GET: AdminManage/ClientContracts/{ClientID}
        public async Task<ActionResult> ClientUsers(int? id)
        {
            logger.SetControllerAction("AdminManageController", "ClientUsers");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad Request");
            }

            logger.Info("Get users for clientId: " + id.ToString());
            var users = db.Users.Where(u => u.ClientID == id && !u.IsCanceled).ToList();

            if (users == null)
            {
                logger.Warn("Users not found");
                throw new HttpException(400, "Bad Request");
            }

            List<ClientUsersViewModel> model = new List<ClientUsersViewModel>();

            model = users
                .Select(u => new ClientUsersViewModel() {
                    UserID = u.Id,
                    Username = u.UserName,
                    PhoneNumber = u.PhoneNumber
                })
                .ToList();

            var client = await db.Client.FindAsync(id);

            ViewBag.ClientName = client.Name;
            logger.Info("Return users list");
            return View(model);
        }

        public async Task<ActionResult> CreateUser(int? id)
        {
            logger.SetControllerAction("AdminManageController", "CreateUser");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad Request");
            }
            logger.Info("Create user for clientId: " + id.ToString());
            var client = await db.Client.FindAsync(id);

            if(client == null)
            {
                logger.Warn("Client not found");
                throw new HttpException(400, "Bad Request");
            }

            CreateClientUserViewModel model = new CreateClientUserViewModel()
            {
                ClientId = client.ClientID
            };

            ViewBag.ClientName = client.Name;
            logger.Info("Show form for create user");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateUser(CreateClientUserViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "CreateUser");
            if (!ModelState.IsValid)
            {
                logger.Warn("Invalid form data");
                return View(model);
            }

            logger.Info("Find client with id: " + model.ClientId);
            var client = await db.Client.FindAsync(model.ClientId);

            if(client == null)
            {
                logger.Warn("Client not found");
                throw new HttpException(400, "Bad Request");
            }
            logger.Info("Check user existens for username: " + model.Username);
            var user = await UserManager.FindByNameAsync(model.Username);

            if(user == null)
            {
                logger.Info("User not found. Creating user with username: " + model.Username);
                var newUser = new ApplicationUser()
                {
                    ClientID = client.ClientID,
                    UserName = model.Username,
                    IsCanceled = false,
                    IsDeleted = false,
                    PhoneNumber = model.PhoneNumber,
                    PhoneNumberConfirmed = true,
                    TwoFactorEnabled = true
                };

               var createResult = await UserManager.CreateAsync(newUser, model.Password);
                if (!createResult.Succeeded)
                {
                    foreach (var error in createResult.Errors)
                    {
                        logger.Error("User creation failed: " + error);
                        ModelState.AddModelError("", error);
                    }

                    return View(model);
                }
                var result = await UserManager.SetLockoutEnabledAsync(newUser.Id, true);
                logger.Info("User created");

                var roles = UserManager.AddToRole(newUser.Id, "User");
                logger.Info("Role User added");
            }
            else if(user.IsCanceled && !user.IsDeleted && user.ClientID == client.ClientID)
            {
                logger.Info("User found at same client. Unlocking user with username: " + model.Username);
                user.IsCanceled = false;
                user.IsDeleted = false;
                user.PasswordHash = UserManager.PasswordHasher.HashPassword(model.Password);
                UserManager.Update(user);
            }
            else if(user.IsDeleted)
            {
                logger.Info("User found at different client. Unlocking user with username: " + model.Username + " and set new clientId");
                user.IsCanceled = false;
                user.IsDeleted = false;
                user.PasswordHash = UserManager.PasswordHasher.HashPassword(model.Password);
                user.ClientID = client.ClientID;
                UserManager.Update(user);
            }

            TempData["StatusMessage"] = Resources.Resources.UserCreatedSuccessfully;

            return RedirectToAction("ClientUsers", new { id = client.ClientID });
        }

        public async Task<ActionResult> EditUser(string id)
        {
            logger.SetControllerAction("AdminManageController", "EditUser");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad request");
            }

            logger.Info("Find user with userId: " + id);
            var user = await UserManager.FindByIdAsync(id);

            if(user == null)
            {
                logger.Warn("user not found");
                throw new HttpException(404, "Not Found");
            }

            EditUserViewModel model = new EditUserViewModel()
            {
                UserID = user.Id,
                Username = user.UserName,
                PhoneNumber = user.PhoneNumber,
                ClientID = user.ClientID
            };

            ViewBag.ClientName = user.Client.Name;
            logger.Info("Show user");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditUser(EditUserViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "EditUser");
            if (!ModelState.IsValid)
            {
                logger.Warn("Invalid form data");
                return View(model);
            }
            logger.Info("find User");
            var user = await UserManager.FindByIdAsync(model.UserID);

            user.PhoneNumber = model.PhoneNumber;

            await UserManager.UpdateAsync(user);

            db.SaveChanges();
            logger.Info("Phone number changed to: " + model.PhoneNumber);

            TempData["StatusMessage"] = Resources.Resources.UserUpdatedSuccessfully;

            return RedirectToAction("ClientUsers", new { id = user.ClientID });
        }


        #endregion

        #region Groups
        //GET: /AdminManage/Groups/
        public ActionResult Groups()
        {
            logger.SetControllerAction("AdminManageController", "Groups");
            //var modelQry = (from groups in db.Group
            //                join group_numbers in db.GroupNumbers on groups.GroupID equals group_numbers.GroupID
            //                into group_number
            //                //where groups.Default == true
            //                from gn in group_number.DefaultIfEmpty()
            //                group gn by groups.GroupID into grouped
            //                select new
            //                {
            //                    groupId = grouped.Key,
            //                    totalNumbers = grouped.Count(t => t.NumberID != null && t.Numbers.Active)
            //                }).ToList();

            //List<AdminManageGroupsViewModel> model = new List<AdminManageGroupsViewModel>();

            //foreach (var item in modelQry)
            //{
            //    var group = db.Group.Find(item.groupId);
            //    model.Add(new AdminManageGroupsViewModel()
            //    {
            //        GroupID = item.groupId,
            //        ClientName = db.Client.Find(group.ClientID).Name,
            //        //Name = group.Name,
            //        TotalOfNumbers = item.totalNumbers
            //    });
            //}
            

            //upit vadi klijente i count njegovih brojeva ukoliko postoje
            List<AdminManageGroupsViewModel> model = db.Numbers
            .GroupBy(n => n.Clients)
            //.Where(n => n.Key.Numbers.Count)
            .Select(n => new AdminManageGroupsViewModel
            {
                ClientID = n.Key.ClientID,
                ClientName = n.Key.Name,
                TotalOfNumbers = n.Count(a => a.Active)
            })
            .ToList();
            logger.Info("Get numbers in groups");
            //upit vadi klijente koji jos nemaju nijedan broj - imaju formiranu VPN grupu u tabeli GROUPS ali u toj grupi nemaju brojeve pa zato nemaju taj GroupID u GROUP_NUMBERS. Uslov g.Default == true je dodat samo da bi ubrzao izvrsavanje upita (da ne bi proveravao svaku grupu za jednog klijenta)
            var clientsWithNoNumbersInVPNGroup = db.Group.Where(g => g.Default == true && !db.GroupNumbers.Where(gn => gn.GroupID == g.GroupID).Any())
                                                    .Select(c => c.ClientID)
                                                    .Distinct()
                                                    .ToList();
            logger.Info("Get groups with no numbers");
            //ovaj upit ce ostaviti samo klijente kojih vec nema u model-u
            var clientsNotExistingInModel = clientsWithNoNumbersInVPNGroup.Where(c => !model.Where(m => m.ClientID == c).Any()).ToList();

            var name = "";
            foreach (var client in clientsNotExistingInModel)
            {
                name = db.Client.Where(c => c.ClientID == client)
                                .Select(n => n.Name)
                                .FirstOrDefault()
                                .ToString();

                model.Add(new AdminManageGroupsViewModel
                {
                    ClientID = client,
                    ClientName = name,
                    TotalOfNumbers = 0
                });
            }
            logger.Info("Show clients");
            return View(model);
        }

        //GET: /AdminManage/CreateGroup
        //public ActionResult CreateGroup()
        //{
        //    AdminCreateGroupViewModel model = new AdminCreateGroupViewModel();
        //    model.Clients = new SelectList(db.Client, "ClientID", "Name", 1);
            
        //    return View(model);
        //}

        //POST: /AdminManage/CreateGroup
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult CreateGroup(AdminCreateGroupViewModel model)
        //{
        //    GroupModel group = new GroupModel()
        //    {
        //        ClientID = model.ClientID,
        //        Name = model.Name,
        //        InsertDate = DateTime.Now
        //    };

        //    db.Group.Add(group);
        //    db.SaveChanges();

        //    return RedirectToAction("EditGroup", "AdminManage", new { id = group.GroupID });
        //}

        //GET: /AdminManage/EditGroup
        //public ActionResult EditGroup(int? id)
        //{
        //    if(id == null)
        //    {
        //        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
        //    }

        //    var group = db.Group.Find(id);
        //    AdminEditGroupViewModel model = new AdminEditGroupViewModel()
        //    {
        //        GroupID = group.GroupID,
        //        Name = group.Name
        //    };

        //    model.Clients = new SelectList(db.Client, "ClientID", "Name", 1);
        //    model.ClientID = group.ClientID;

        //    return View(model);
        //}

        //POST: /AdminManage/EditGroup
        //[HttpPost]
        //[ValidateAntiForgeryToken]
        //public ActionResult EditGroup(AdminEditGroupViewModel model)
        //{
        //    if(ModelState.IsValid)
        //    {
        //        GroupModel group = new GroupModel()
        //        {
        //            GroupID = model.GroupID,
        //            ClientID = model.ClientID,
        //            Name = model.Name,
        //            InsertDate = DateTime.Now
        //        };

        //        db.Entry(group).State = System.Data.Entity.EntityState.Modified;
        //        db.SaveChanges();

        //        return RedirectToAction("Groups", "AdminManage", new { searchTerm = db.Client.Find(model.ClientID).Name });
        //    }

        //    return View(model);
        //}

        #endregion

        #region Numbers
        //GET: /AdminManage/Numbers/{ClientID}
        public ActionResult Numbers(int? id)
        {
            logger.SetControllerAction("AdminManageController", "Numbers");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad Request");
            }
            logger.Info("Find client with id: " + id.ToString());
            var client = db.Client.Find(id);
            var clientName = client.Name.ToString();
            var groupIdVPN = client.Groups.Where(g => g.Default).Select(g => g).FirstOrDefault();

            //ViewBag.GroupName = groupIdVPN.Name;
            ViewBag.ClientID = id;
            ViewBag.ClientName = clientName;
            ViewBag.NumCount = groupIdVPN.GroupNumbers.Count;
            logger.Info("Show number counts");
            return View();
        }

        public ActionResult ToggleLockNumber(int? id, int? clientId)
        {
            logger.SetControllerAction("AdminManageController", "ToggleLockNumber");
            logger.Info("Find number with id: " + id.ToString());
            var number = db.Numbers.Find(id);

            if (number == null || clientId == null)
            {
                logger.Warn("number or clientId is null");
                throw new HttpException(400, "Bad Request");
            }

            number.SendAllowed = !number.SendAllowed;
            db.SaveChanges();
            logger.Info("Number is now locked: " + !number.SendAllowed);
            return RedirectToAction("Numbers", "AdminManage", new { id = clientId });
        }

        //GET: /AdminManage/CreateNumber/{GroupID}
        public ActionResult CreateNumber(int id)
        {
            logger.SetControllerAction("AdminManageController", "CreateNumber");
            AdminCreateNumberViewModel model = new AdminCreateNumberViewModel();
            logger.Info("Get client with clientId: " + id.ToString());
            var client = db.Client.Find(id);
            model.ClientID = id;

            //pronalazi VPN grupu
            model.GroupID = client.Groups.Where(g => g.Default).Select(g => g.GroupID).FirstOrDefault();

            model.NumberType = new SelectList(db.NumberType.Where(nt => nt.Name == "VPN"), "NumberTypeID", "Name", 1);
            
            model.SendAllowed = true;

            logger.Info("Show form for add number");

            return PartialView(model);
        }

        //POST: /AdminManage/CreateNumber/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateNumber(AdminCreateNumberViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "CreateNumber");
            logger.Info("Get group with groupId: " + model.GroupID);
            var group = db.Group.Find(model.GroupID);
            string groupName = group.Name;

            if ((model.NumberTypeID == (int)NumberType.VPN && groupName != "VPN") || (model.NumberTypeID != (int)NumberType.VPN && groupName == "VPN"))
            {
                logger.Warn("Problem with number: " + Resources.Resources.NumberTypeIsNotBelong);
                ModelState.AddModelError("NumberTypeID", Resources.Resources.NumberTypeIsNotBelong);
            }

            if (ModelState.IsValid)
            {
                NumbersModel number = new NumbersModel()
                {
                    ClientID = model.ClientID,
                    NumberTypeID = model.NumberTypeID,
                    Number = model.Number,
                    Name = model.Name,
                    SendAllowed = model.SendAllowed,
                    Active = true,
                    InsertDate = DateTime.Now
                };

                db.Numbers.Add(number);

                GroupNumberModel group_number = new GroupNumberModel()
                {
                    GroupID = model.GroupID,
                    NumberID = number.NumberID,
                    InsertDate = DateTime.Now
                };

                db.GroupNumbers.Add(group_number);

                db.SaveChanges();
                logger.Info("Number successfully added");
                return RedirectToAction("Numbers", "AdminManage", routeValues: new { id = model.ClientID });
            }

            if (group.Name == "VPN")
                model.NumberType = new SelectList(db.NumberType.Where(nt => nt.Name == "VPN"), "NumberTypeID", "Name", 1);
            else
                model.NumberType = new SelectList(db.NumberType.Where(nt => nt.Name != "VPN"), "NumberTypeID", "Name", 1);

            return View(model);
        }

        //GET: /AdminManage/EditNumber/{NumberID}
        public ActionResult EditNumber(int id)
        {
            logger.SetControllerAction("AdminManageController", "EditNumber");
            logger.Info("Find number with id: " + id.ToString());
            var number = (from gnumbers in db.Numbers
                           where gnumbers.NumberID == id
                           select new AdminEditNumberViewModel()
                           {
                               ClientID = gnumbers.Clients.ClientID,
                               NumberID = gnumbers.NumberID,
                               NumberTypeID = gnumbers.NumberType.NumberTypeID,
                               Number = gnumbers.Number,
                               Name = gnumbers.Name,
                               SendAllowed = gnumbers.SendAllowed
                           }).First();
            
            AdminEditNumberViewModel model = new AdminEditNumberViewModel();
            
            model.NumberType = new SelectList(db.NumberType, "NumberTypeID", "Name", 1);
            model.ClientID = number.ClientID;
            //model.GroupID = number.GroupID;
            model.NumberID = number.NumberID;
            model.NumberTypeID = number.NumberTypeID;
            model.Number = number.Number;
            model.Name = number.Name;
            model.SendAllowed = number.SendAllowed;

            logger.Info("Show edit number form");

            return PartialView(model);
        }

        //POST: /AdminManage/EditNumber/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditNumber(AdminEditNumberViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "EditNumber");
            
            if (ModelState.IsValid)
            {
                //var group = db.Group.Find(model.GroupID);
                //string groupName = group.Name;
                //int newGroupId = -1;

                //if (model.NumberTypeID == (int)NumberType.VPN && groupName != "VPN")
                //{
                //    newGroupId = db.Group.Where(g => g.GroupID != group.GroupID && g.ClientID == group.ClientID && g.Name == "VPN")
                //        .FirstOrDefault()
                //        .GroupID;
                //}

                //if (model.NumberTypeID != (int)NumberType.VPN && groupName == "VPN")
                //{
                //    newGroupId = db.Group.Where(g => g.GroupID != group.GroupID && g.ClientID == group.ClientID && g.Name == "VAN VPN")
                //        .FirstOrDefault()
                //        .GroupID;
                //}
                logger.Info("Edit number with id: " + model.NumberID.ToString() + " for clientId: " + model.ClientID.ToString());
                NumbersModel number = new NumbersModel()
                {
                    ClientID = model.ClientID,
                    NumberID = model.NumberID,
                    NumberTypeID = model.NumberTypeID,
                    Number = model.Number,
                    Name = model.Name,
                    SendAllowed = model.SendAllowed,
                    InsertDate = DateTime.Now
                };

                db.Entry(number).State = System.Data.Entity.EntityState.Modified;

                //if (newGroupId != -1)
                //{
                //    var groupNumber = db.GroupNumbers.Find(model.GroupID, model.NumberID);
                //    db.Entry(groupNumber).State = System.Data.Entity.EntityState.Deleted;

                //    var newGroupNumber = new GroupNumberModel()
                //    {
                //        GroupID = newGroupId,
                //        NumberID = model.NumberID
                //    };

                //    db.GroupNumbers.Add(newGroupNumber);
                //}
                
                db.SaveChanges();

                logger.Info("Number saved with data: numberTypeId - " + model.NumberTypeID + " Number - " + model.Number + " Name - " + model.Name + " SendAllowed - " + model.SendAllowed);

                return RedirectToAction("Numbers", "AdminManage", routeValues: new { id = model.ClientID });
            }

            model.NumberType = new SelectList(db.NumberType, "NumberTypeID", "Name", 1);

            return View(model);
        }

        #endregion

        #region Alphanumerics
        // GET: /AdminManage/Alphanumerics/{ClientID}
        public ActionResult Alphanumerics(int? id)
        {
            logger.SetControllerAction("AdminManageController", "Alphanumerics");
            
            if (id == null)
            {
                logger.Warn("id is null");
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            logger.Info("Get client with id: " + id.ToString());
            ViewBag.Client = db.Client.Find(id).Name;
            List<AlphanumericModel> model = db.Alphanumeric.Where(a => a.ClientID == id).ToList();

            return View(model);
        }

        //GET: /AdminManage/EditAlphanumeric/{AlphanumericID}
        public ActionResult EditAlphanumeric(int id)
        {
            logger.SetControllerAction("AdminManageController", "EditAlphanumeric");
            logger.Info("Get alphanumeric with id: " + id.ToString());
            AlphanumericModel alphanumeric = db.Alphanumeric.Find(id);

            AlphanumericViewModel model = new AlphanumericViewModel()
            {
                AlphanumericID = alphanumeric.AlphanumericID,
                Alphanumeric = alphanumeric.Alphanumeric,
                ClientID = alphanumeric.ClientID
            };

            if (model == null)
            {
                return HttpNotFound();
            }
            logger.Info("Show edit alphanumeric form");
            return PartialView(model);
        }

        //POST: /AdminManage/EditAlphanumeric/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditAlphanumeric(AlphanumericViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "EditAlphanumeric");
            if (ModelState.IsValid)
            {
                logger.Info("Change data for alphanumericId: " + model.AlphanumericID);
                AlphanumericModel alphModel = new AlphanumericModel()
                {
                    AlphanumericID = model.AlphanumericID,
                    Alphanumeric = model.Alphanumeric,
                    ClientID = model.ClientID,
                    InsertDate = DateTime.Now
                };

                db.Entry(alphModel).State = System.Data.Entity.EntityState.Modified;
                db.SaveChanges();
                logger.Info("Alphanumeric new data: Alphanumeric - " + model.Alphanumeric + " ClientID - " + model.ClientID.ToString());
            }
            return RedirectToAction("Alphanumerics", routeValues: new { id = model.ClientID });
        }

        //GET: /AdminManage/CreateAlphanumeric/{ClientID}
        public ActionResult CreateAlphanumeric(int id)
        {
            logger.SetControllerAction("AdminManageController", "CreateAlphanumeric");
            AlphanumericCreateViewModel model = new AlphanumericCreateViewModel()
            {
                ClientID = id
            };
            logger.Info("Create aplhanumeric for clientId: " + id.ToString());

            return PartialView(model);
        }

        //POST: /AdminManage/CreateAlphanumeric/
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateAlphanumeric(AlphanumericCreateViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "CreateAlphanumeric");
            if (ModelState.IsValid)
            {
                AlphanumericModel alphModel = new AlphanumericModel()
                {
                    Alphanumeric = model.Alphanumeric,
                    ClientID = model.ClientID,
                    InsertDate = DateTime.Now
                };

                db.Alphanumeric.Add(alphModel);
                db.SaveChanges();
                logger.Info("Alphanumeric: " + model.Alphanumeric + " for clientId: " + model.ClientID + " successfully saved");
            }
            return RedirectToAction("Alphanumerics", routeValues: new { id = model.ClientID });
        }
        #endregion

        #region MessageCost
        //GET: /AdminManage/MessageCost/
        public ActionResult MessageCost()
        {
            logger.SetControllerAction("AdminManageController", "MessageCost");
            IEnumerable<MessageCostModel> costModel = db.MessageCost.ToList();

            List<MessageCostListViewModel> costViewModel = new List<MessageCostListViewModel>();
            logger.Info("Get message cost data");
            var model = costModel.Where(cm => cm.EndDate == null)
                .OrderBy(cm => cm.NumberOfMessagesFrom).OrderBy(cm => cm.NumberTypeID)
                .GroupBy(cm => cm.NumberTypeID)
                .Select(cm => new MessageCostListViewModel()
                {
                    NumberType = db.NumberType.Find(cm.Key).Name,
                    MessageCosts = cm
                });

            return View(model);
        }

        //GET: /AdminManage/EditMessageCost/
        public ActionResult EditMessageCost(int id)
        {
            logger.SetControllerAction("AdminManageController", "EditMessageCost");
            var model = from mc in db.MessageCost
                              join nt in db.NumberType
                              on mc.NumberTypeID equals nt.NumberTypeID
                              where mc.MessageCostID == id
                              select new EditMessageCostViewModel()
                              {
                                  MessageCostID = mc.MessageCostID,
                                  NumberOfMessagesFrom = mc.NumberOfMessagesFrom,
                                  NumberOfMessagesTo = mc.NumberOfMessagesTo,
                                  Price = mc.Price,
                                  NumberTypeID = mc.NumberTypeID,
                                  NumberType = nt.Name
                              };

            logger.Info("Get message cost for messageCostId: " + id.ToString());

            if (model.Count() == 0 || model == null)
            {
                return RedirectToAction("MessageCost");
            }

            return View(model.FirstOrDefault());
        }

        //POST: /AdminManage/EditMessageCost/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditMessageCost(EditMessageCostViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "EditMessageCost");
            if (ModelState.IsValid)
            {
                logger.Info("Get message cost for messageCostId: " + model.MessageCostID.ToString());
                var messageCostOld = db.MessageCost.Find(model.MessageCostID);

                if(model.Equals(messageCostOld))
                {
                    logger.Info("Nothing chaged. Return to MessageCost list");
                    return RedirectToAction("MessageCost");
                }

                messageCostOld.EndDate = DateTime.Now;

                MessageCostModel messageCost = new MessageCostModel
                {
                    NumberOfMessagesFrom = model.NumberOfMessagesFrom,
                    NumberOfMessagesTo = model.NumberOfMessagesTo,
                    NumberTypeID = model.NumberTypeID,
                    Price = model.Price,
                    StartDate = DateTime.Now,
                    EndDate = null,
                    InsertDate = DateTime.Now
                };

                db.MessageCost.Add(messageCost);

                db.SaveChanges();
                logger.Info("Cost changed to: NumberOfMessagesFrom - " + model.NumberOfMessagesFrom.ToString() + " NumberOfMessagesTo - " + model.NumberOfMessagesTo + " Price - " + model.Price + " NumberTypeID " + model.NumberTypeID);
                return RedirectToAction("MessageCost");            
            }

            return View(model);
        }

        //GET: /AdminManage/CreateMessageCost
        public ActionResult CreateMessageCost()
        {
            logger.SetControllerAction("AdminManageController", "CreateMessageCost");
            CreateMessageCostViewModel model = new CreateMessageCostViewModel();
            model.NumberTypes = new SelectList(db.NumberType, "NumberTypeID", "Name", 1);
            logger.Info("Show form for create message cost");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateMessageCost(CreateMessageCostViewModel model)
        {
            logger.SetControllerAction("AdminManageController", "CreateMessageCost");
            if (ModelState.IsValid)
            {
                MessageCostModel mcModel = new MessageCostModel()
                {
                    NumberOfMessagesFrom = model.NumberOfMessagesFrom,
                    NumberOfMessagesTo = model.NumberOfMessagesTo,
                    Price = model.Price,
                    NumberTypeID = model.NumberTypeID,
                    StartDate = DateTime.Now,
                    InsertDate = DateTime.Now
                };

                db.MessageCost.Add(mcModel);

                db.SaveChanges();
                logger.Info("New message cost created: NumberOfMessagesFrom - " + model.NumberOfMessagesFrom.ToString() + " NumberOfMessagesTo - " + model.NumberOfMessagesTo + " Price - " + model.Price + " NumberTypeID " + model.NumberTypeID);

                return RedirectToAction("MessageCost");
            }

            model.NumberTypes = new SelectList(db.NumberType, "NumberTypeID", "Name", 1);

            return View(model);
        }
        #endregion

        #region FileUpload
        //GET /AdminManage/UploadNumbers/{groupID}
        public ActionResult UploadNumbers(int? id)
        {
            logger.SetControllerAction("AdminManageController", "UploadNumbers");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad Request");
            }
            logger.Info("Get group with id: " + id.ToString());
            var group = db.Group.Find(id);

            ViewBag.GroupName = group.Name;
            ViewBag.ClientName = group.Client.Name;

            return View();
        }

        //POST /AdminManage/CheckUploadedNumbers/{GroupID}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CheckUploadedNumbers(int? id, HttpPostedFileBase file)
        {
            logger.SetControllerAction("AdminManageController", "CheckUploadedNumbers");
            if (id == null)
            {
                logger.Warn("id is null");
                throw new HttpException(400, "Bad Request");
            }

            logger.Info("Get group with id: " + id.ToString());
            var group = db.Group.Find(id);

            ViewBag.GroupName = group.Name;
            ViewBag.ClientName = group.Client.Name;

            string filename = id.ToString() + " " + DateTime.Now.ToString("yyyyMMddhhmmss");
            string pathToSave = Server.MapPath("~/UploadedFiles");

            if(file.ContentLength > 0)
            {
                logger.Info("Save file with name " + filename);
                file.SaveAs(System.IO.Path.Combine(pathToSave, filename));
                logger.Info("File saved");
            }
            else
            {
                logger.Warn("File is empty");
                throw new HttpException(400, "Empty file");
            }

            string filePath = Server.MapPath("~/UploadedFiles/" + filename);
            logger.Info("Get data from excel");
            DataTable dtData = GetDataFromExcel(filePath);
            string badNumbers = "";
            string existingNumbers = "";
            string badGroup = "";
            logger.Info("Check data in excel");
            AdminConfirmUploadFileViewModel model = CheckDataFromExcel((int)id, dtData, out badNumbers, out existingNumbers, out badGroup);

            var clientId = db.Group.Where(g => g.GroupID == id).Select(g => g.ClientID).FirstOrDefault();
            int count = 1;
            
            db.Database.ExecuteSqlCommand("DELETE FROM BST_TEMP_IMPORT");
            logger.Info("Delete data in temp table in db");
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

            db.SaveChanges();
            logger.Info("Data successfully imported to temp table");

            ViewBag.ExistingNumbers = existingNumbers.Length > 2 ? existingNumbers.Remove(existingNumbers.Length - 2) : "";
            ViewBag.BadNumbers = badNumbers.Length > 2 ? badNumbers.Remove(badNumbers.Length - 2) : "";
            ViewBag.BadGroup = badGroup.Length > 2 ? badGroup.Remove(badGroup.Length - 2) : "";

            return View();
        }

        //POST /AdminManage/ConfirmUploadNumbers/{GroupID}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmUploadNumbers(int? id)
        {
            logger.SetControllerAction("AdminManageController", "ConfirmUploadNumbers");
            if (id != null)
            {
                System.Data.SqlClient.SqlParameter groupIdParam = new System.Data.SqlClient.SqlParameter("@GroupID", id.ToString());
                db.Database.ExecuteSqlCommand("[dbo].[sp_insertDataFromTempImport] @GroupID", groupIdParam);
                logger.Info("Data imported successfully");
            }

            return RedirectToAction("Numbers", "AdminManage", new { id = id });
        }

        #endregion

        #region FileDowload

        public FileResult DownloadManual()
        {
            logger.SetControllerAction("AdminManageController", "ConfirmUploadNumbers");
            logger.Info("Downloading manual");
            return File("~/UploadedFiles/KorisnickoUputstvoBizSMS_administrator.pdf", "application/pdf");
        }

        #endregion

        #region CustomMethods
        /// <summary>
        /// For given client (clientID) return user with "Client" role from users table 
        /// </summary>
        /// <param name="client"></param>
        /// <returns></returns>
        private async Task<ApplicationUser> GetClientUser(ClientModel client)
        {
            var users = UserManager.Users.Where(c => c.ClientID == client.ClientID).ToList();
            ApplicationUser selectedUser = null;
            try
            {
                foreach (var user in users)
                {
                    if (await UserManager.IsInRoleAsync(user.Id, "Client"))
                    {
                        selectedUser = user;
                        break;
                    }
                }
            }
            catch(Exception ex)
            {
                return null;
            }

            return selectedUser;
        }

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


                for (int row = startCell.Row+1; row <= endCell.Row; row++)
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

        private AdminConfirmUploadFileViewModel CheckDataFromExcel(int id, DataTable dtData, out string badNumbers, out string existingNumbers, out string badGroup)
        {
            AdminConfirmUploadFileViewModel model = new AdminConfirmUploadFileViewModel();
            model.Numbers = new List<AdminUploadFileViewModel>();

            badNumbers = "";
            existingNumbers = "";
            badGroup = "";
            
            var group = db.Group.Find(id);
            int clientID = group.ClientID;
            string numberType;

            //LDAPSearcher ldap = new LDAPSearcher();
            //ldapCheckWS.LDAPCheck ldap = new ldapCheckWS.LDAPCheck();
            SelfCareLdapWS.LDAPCheckSoapClient ldap = new SelfCareLdapWS.LDAPCheckSoapClient();

            foreach (DataRow row in dtData.Rows)
            {
                string numberFromFile = row.Field<string>("Number").Trim();
                string nameFromFile = row.Field<string>("Name").Trim();

                numberType = "";

                //provera da li broj postoji kod odabranog klijenta
                var numberExists = db.Numbers.Where(
                    number => 
                    number.Number == numberFromFile && number.ClientID == clientID
                )
                .FirstOrDefault();

                if (numberExists == null)
                {
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

                            //SelfCareLdapWS.vratiTipKorisnikaResponseBody resp = new SelfCareLdapWS.vratiTipKorisnikaResponseBody();
                            //resp = ldap.vratiTipKorisnika(resp);

                            var result = ldap.vratiTipKorisnika(req.msisdn);
                            if (result != null)
                            {
                                if (group.Name == "VPN")
                                {
                                    numberType = "VPN";
                                }
                                else if (group.Name == "VAN VPN")
                                {
                                    numberType = "U MTS";
                                }
                            }
                            else
                            {
                                if (group.Name == "VPN")
                                {
                                    badGroup += numberFromFile + ", ";
                                }
                                else if (group.Name == "VAN VPN")
                                {
                                    numberType = "VAN MTS";
                                }
                            }
                            //if (ldap.vratiTipKorisnika("381" + numberFromFile.Remove(0, 1).Trim()) != null)
                            //{
                            //    if(group.Name == "VPN")
                            //    {
                            //        numberType = "VPN";
                            //    }
                            //    else if(group.Name == "VAN VPN")
                            //    {
                            //        numberType = "U MTS";
                            //    }
                            //}
                            //else
                            //{
                            //    if (group.Name == "VPN")
                            //    {
                            //        badGroup += numberFromFile + ", ";
                            //    }
                            //    else if (group.Name == "VAN VPN")
                            //    {
                            //        numberType = "VAN MTS";
                            //    }
                            //}
                        }
                        #endregion

                        if (numberType != "")
                        {
                            model.Numbers.Add(new AdminUploadFileViewModel()
                            {
                                Name = nameFromFile,
                                Number = numberFromFile,
                                NumberType = numberType
                            });
                        }
                    }
                    else
                    {
                        badNumbers += numberFromFile + ", ";
                    }
                }
                else
                {
                    existingNumbers += numberFromFile + ", ";
                }
            }
            return model;
        }
        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}