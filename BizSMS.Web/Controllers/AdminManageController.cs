#nullable disable
using BizSMS.Web.Data;
using BizSMS.Web.Filters;
using BizSMS.Web.Helpers;
using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Web.Controllers
{
    // Zamena za legacy BizSMS.Controllers.AdminManageController.
    // Administratorski CRUD: klijenti, ugovori, korisnici, brojevi, alfanumerici, cene poruka, uvoz brojeva.
    [AuthorizeUser(Roles = "Administrator")]
    public class AdminManageController : BaseController
    {
        private readonly IWebHostEnvironment _env;

        public AdminManageController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env)
            : base(db, userManager)
        {
            _env = env;
        }

        private string UploadedFilesPath => Path.Combine(_env.ContentRootPath, "UploadedFiles");

        #region Clients
        public IActionResult Index()
        {
            return RedirectToAction("ManageClients");
        }

        // GET: /AdminManage/ManageClients/{ClientID} — ako je id prisutan, prebacuje IsCanceled klijenta i njegovih korisnika
        public IActionResult ManageClients(int? id)
        {
            if (id != null)
            {
                var toggleClient = db.Client.Find(id);
                toggleClient.IsCanceled = !toggleClient.IsCanceled;

                var users = db.Users.Where(u => u.ClientID == id && u.IsDeleted == false).ToList();
                foreach (var user in users)
                {
                    user.IsCanceled = !user.IsCanceled;
                }

                db.SaveChanges();
            }

            var clients = db.Client.Where(c => c.Name != "Telekom").ToList();
            List<AdminManageClientsViewModel> model = new List<AdminManageClientsViewModel>();

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
        public IActionResult CreateClient(string message)
        {
            ViewBag.StatusMessage = message;
            return View();
        }

        // POST: /AdminManage/CreateClient
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateClient(AdminCreateClientViewModel model)
        {
            if (ModelState.IsValid)
            {
                var existingClient = db.Client.FirstOrDefault(c => c.Name == model.ClientName);

                if (existingClient == null)
                {
                    var contracts = new List<ClientContractsModel>
                    {
                        new ClientContractsModel() { ContractId = model.ContractID, SynchronizationDate = new DateTime(1900, 1, 1) }
                    };

                    ClientModel client = new ClientModel()
                    {
                        MtsID = model.Mts_ID,
                        Name = model.ClientName,
                        PhoneNumber = model.PhoneNumber,
                        IsCanceled = false,
                        Contracts = contracts,
                        InsertDate = DateTime.Now
                    };

                    // Snimanje klijenta pre kreiranja korisnika/grupe da bismo dobili generisani Client_ID.
                    // (Legacy se oslanjao na deljeni OWIN kontekst; u EF Core FK vrednosti kopiramo posle snimanja.)
                    db.Client.Add(client);
                    db.SaveChanges();

                    ApplicationUser user = new ApplicationUser()
                    {
                        UserName = model.Username,
                        ClientID = client.ClientID,
                        PhoneNumber = model.PhoneNumber
                    };

                    var result = await UserManager.CreateAsync(user, model.Password);
                    if (!result.Succeeded)
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError("", error.Description);
                        }
                        return View(model);
                    }

                    await UserManager.SetLockoutEnabledAsync(user, true);
                    await UserManager.AddToRoleAsync(user, "Client");

                    GroupModel vpnGroup = new GroupModel()
                    {
                        ClientID = client.ClientID,
                        Name = "VPN",
                        Default = true,
                        InsertDate = DateTime.Now
                    };
                    db.Group.Add(vpnGroup);

                    AlphanumericModel alphanumeric = new AlphanumericModel()
                    {
                        Alphanumeric = model.PhoneNumber,
                        ClientID = client.ClientID,
                        InsertDate = DateTime.Now
                    };
                    db.Alphanumeric.Add(alphanumeric);

                    db.SaveChanges();

                    // Prevlačenje VPN brojeva iz TIS za zadati ugovor odmah po kreiranju klijenta.
                    try
                    {
                        db.Database.ExecuteSqlRaw("EXEC dbo.sp_InsertNumbers {0}", model.ContractID);
                    }
                    catch (SqlException ex)
                    {
                        logger.Error(ex.Message);
                    }

                    return RedirectToAction("Alphanumerics", new { id = client.ClientID });
                }
            }
            return View(model);
        }

        // GET: /AdminManage/EditClient/{ClientID}
        public IActionResult EditClient(int? id)
        {
            if (id == null)
                return BadRequest();

            ClientModel client = db.Client.Find(id);
            if (client == null)
                return BadRequest();

            AdminEditClientViewModel model = new AdminEditClientViewModel()
            {
                ClientID = client.ClientID,
                Mts_ID = client.MtsID,
                ClientName = client.Name,
                ContractID = string.Join(", ", client.Contracts.Where(cc => cc.ClientId == id).Select(c => c.ContractId).ToList()),
                Username = string.Join(", ", client.ApplicationUsers.Where(au => au.IsDeleted == false).Select(u => u.UserName).ToList()),
                PhoneNumber = client.PhoneNumber
            };

            return View(model);
        }

        // POST: /AdminManage/EditClient
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditClient(AdminEditClientViewModel model)
        {
            if (ModelState.IsValid)
            {
                ClientModel client = db.Client.Find(model.ClientID);
                client.MtsID = model.Mts_ID;
                client.Name = model.ClientName;
                client.PhoneNumber = model.PhoneNumber;

                db.SaveChanges();

                return RedirectToAction("ManageClients");
            }

            return View(model);
        }

        // GET: /AdminManage/ClientContracts/{ClientID}
        public async Task<IActionResult> ClientContracts(int? id)
        {
            if (id == null)
                return BadRequest();

            var client = await db.Client.FindAsync(id);
            if (client == null)
                return BadRequest();

            ClientContractsViewModel model = new ClientContractsViewModel()
            {
                ClientId = client.ClientID,
                ClientName = client.Name
            };

            return View(model);
        }

        // GET: /AdminManage/AddClientContract/{ClientID}
        public async Task<IActionResult> AddClientContract(int? id)
        {
            if (id == null)
                return BadRequest();

            var client = await db.Client.FindAsync(id);
            if (client == null)
                return BadRequest();

            AddClientContractViewModel model = new AddClientContractViewModel()
            {
                ClientId = client.ClientID,
                ClientName = client.Name
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddClientContract(AddClientContractViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var client = await db.Client.FindAsync(model.ClientId);

            client.Contracts = new List<ClientContractsModel>();
            client.Contracts.Add(new ClientContractsModel()
            {
                ContractId = model.ContractId,
                SynchronizationDate = new DateTime(1900, 1, 1)
            });

            db.SaveChanges();

            // Postojeći klijent -> RefreshNumbers umesto InsertNumbers.
            try
            {
                db.Database.ExecuteSqlRaw("EXEC dbo.sp_RefreshNumbers {0}", model.ContractId);
            }
            catch (SqlException ex)
            {
                logger.Error(ex.Message);
            }

            return RedirectToAction("ClientContracts", "AdminManage", new { id = client.ClientID });
        }

        // GET: /AdminManage/EditClientContract/{ClientContractsId}
        public async Task<IActionResult> EditClientContract(int? id)
        {
            if (id == null)
                return BadRequest();

            var contract = await db.ClientContract.FindAsync(id);
            if (contract == null)
                return BadRequest();

            EditClientContractViewModel model = new EditClientContractViewModel()
            {
                ClientContractId = contract.ClientContractsId,
                ClientId = contract.ClientId,
                ClientName = contract.Client.Name,
                ContractId = contract.ContractId
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditClientContract(EditClientContractViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var contract = await db.ClientContract.FindAsync(model.ClientContractId);
            contract.ContractId = model.ContractId;

            db.SaveChanges();

            try
            {
                db.Database.ExecuteSqlRaw("EXEC dbo.sp_RefreshNumbers {0}", model.ContractId);
            }
            catch (SqlException ex)
            {
                logger.Error(ex.Message);
            }

            return RedirectToAction("ClientContracts", "AdminManage", new { id = contract.ClientId });
        }
        #endregion

        #region ClientUsers
        // GET: /AdminManage/ClientUsers/{ClientID}
        public async Task<IActionResult> ClientUsers(int? id)
        {
            if (id == null)
                return BadRequest();

            var users = db.Users.Where(u => u.ClientID == id && !u.IsCanceled).ToList();

            List<ClientUsersViewModel> model = users
                .Select(u => new ClientUsersViewModel()
                {
                    UserID = u.Id,
                    Username = u.UserName,
                    PhoneNumber = u.PhoneNumber
                })
                .ToList();

            var client = await db.Client.FindAsync(id);
            ViewBag.ClientName = client.Name;

            return View(model);
        }

        // GET: /AdminManage/CreateUser/{ClientID}
        public async Task<IActionResult> CreateUser(int? id)
        {
            if (id == null)
                return BadRequest();

            var client = await db.Client.FindAsync(id);
            if (client == null)
                return BadRequest();

            CreateClientUserViewModel model = new CreateClientUserViewModel()
            {
                ClientId = client.ClientID
            };

            ViewBag.ClientName = client.Name;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUser(CreateClientUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var client = await db.Client.FindAsync(model.ClientId);
            if (client == null)
                return BadRequest();

            var user = await UserManager.FindByNameAsync(model.Username);

            if (user == null)
            {
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
                        ModelState.AddModelError("", error.Description);
                    }
                    ViewBag.ClientName = client.Name;
                    return View(model);
                }

                await UserManager.SetLockoutEnabledAsync(newUser, true);
                await UserManager.AddToRoleAsync(newUser, "User");
            }
            else if (user.IsCanceled && !user.IsDeleted && user.ClientID == client.ClientID)
            {
                user.IsCanceled = false;
                user.IsDeleted = false;
                user.PasswordHash = UserManager.PasswordHasher.HashPassword(user, model.Password);
                await UserManager.UpdateAsync(user);
            }
            else if (user.IsDeleted)
            {
                user.IsCanceled = false;
                user.IsDeleted = false;
                user.PasswordHash = UserManager.PasswordHasher.HashPassword(user, model.Password);
                user.ClientID = client.ClientID;
                await UserManager.UpdateAsync(user);
            }

            TempData["StatusMessage"] = "Korisnik je uspešno kreiran.";

            return RedirectToAction("ClientUsers", new { id = client.ClientID });
        }

        // GET: /AdminManage/EditUser/{UserID}
        public async Task<IActionResult> EditUser(string id)
        {
            if (id == null)
                return BadRequest();

            var user = await UserManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            EditUserViewModel model = new EditUserViewModel()
            {
                UserID = user.Id,
                Username = user.UserName,
                PhoneNumber = user.PhoneNumber,
                ClientID = user.ClientID
            };

            ViewBag.ClientName = user.Client.Name;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await UserManager.FindByIdAsync(model.UserID);
            user.PhoneNumber = model.PhoneNumber;
            await UserManager.UpdateAsync(user);

            TempData["StatusMessage"] = "Korisnik je uspešno izmenjen.";

            return RedirectToAction("ClientUsers", new { id = user.ClientID });
        }

        // GET: /AdminManage/ResetPassword/{UserID} — administrator resetuje lozinku korisnika klijenta.
        // Zamena za legacy Account/ResetPassword (§6).
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (id == null)
                return BadRequest();

            var user = await UserManager.FindByIdAsync(id);
            if (user == null)
                return NotFound();

            ResetPasswordViewModel model = new ResetPasswordViewModel()
            {
                UserID = user.Id,
                ClientID = user.ClientID,
                Username = user.UserName
            };

            return View(model);
        }

        // POST: /AdminManage/ResetPassword
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await UserManager.FindByIdAsync(model.UserID);
            if (user == null)
                return BadRequest();

            // Nova lozinka u Core formatu (legacy je direktno postavljao PasswordHash preko PasswordHasher-a).
            user.PasswordHash = UserManager.PasswordHasher.HashPassword(user, model.Password);
            var result = await UserManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
                return View(model);
            }

            TempData["StatusMessage"] = "Lozinka je uspešno resetovana.";

            return RedirectToAction("ClientUsers", new { id = user.ClientID });
        }
        #endregion

        #region Groups
        // GET: /AdminManage/Groups — lista klijenata sa brojem njihovih aktivnih brojeva
        public IActionResult Groups()
        {
            List<AdminManageGroupsViewModel> model = db.Numbers
                .GroupBy(n => n.Clients)
                .Select(n => new AdminManageGroupsViewModel
                {
                    ClientID = n.Key.ClientID,
                    ClientName = n.Key.Name,
                    TotalOfNumbers = n.Count(a => a.Active)
                })
                .ToList();

            // Klijenti koji imaju VPN grupu ali još nemaju nijedan broj
            var clientsWithNoNumbersInVPNGroup = db.Group
                .Where(g => g.Default == true && !db.GroupNumbers.Where(gn => gn.GroupID == g.GroupID).Any())
                .Select(c => c.ClientID)
                .Distinct()
                .ToList();

            var clientsNotExistingInModel = clientsWithNoNumbersInVPNGroup.Where(c => !model.Any(m => m.ClientID == c)).ToList();

            foreach (var client in clientsNotExistingInModel)
            {
                var name = db.Client.Where(c => c.ClientID == client).Select(n => n.Name).FirstOrDefault();

                model.Add(new AdminManageGroupsViewModel
                {
                    ClientID = client,
                    ClientName = name,
                    TotalOfNumbers = 0
                });
            }

            return View(model);
        }
        #endregion

        #region Numbers
        // GET: /AdminManage/Numbers/{ClientID}
        public IActionResult Numbers(int? id)
        {
            if (id == null)
                return BadRequest();

            var client = db.Client.Find(id);
            if (client == null)
                return NotFound();

            ViewBag.ClientID = id;
            ViewBag.ClientName = client.Name;
            // Broj se računa agregatom u bazi (SELECT COUNT(*)) umesto preko lazy-load navigacije
            // GroupNumbers.Count, koja bi materijalizovala sve redove grupe u memoriju.
            ViewBag.NumCount = db.GroupNumbers
                .Count(gn => gn.Groups.ClientID == id && gn.Groups.Default);

            return View();
        }

        public IActionResult ToggleLockNumber(int? id, int? clientId)
        {
            var number = db.Numbers.Find(id);

            if (number == null || clientId == null)
                return BadRequest();

            number.SendAllowed = !number.SendAllowed;
            db.SaveChanges();

            return RedirectToAction("Numbers", "AdminManage", new { id = clientId });
        }

        // GET: /AdminManage/CreateNumber/{ClientID}
        public IActionResult CreateNumber(int id)
        {
            AdminCreateNumberViewModel model = new AdminCreateNumberViewModel();
            var client = db.Client.Find(id);
            model.ClientID = id;
            model.GroupID = client.Groups.Where(g => g.Default).Select(g => g.GroupID).FirstOrDefault();
            model.NumberType = new SelectList(db.NumberType.Where(nt => nt.Name == "VPN"), "NumberTypeID", "Name", 1);
            model.SendAllowed = true;

            return View(model);
        }

        // POST: /AdminManage/CreateNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateNumber(AdminCreateNumberViewModel model)
        {
            var group = db.Group.Find(model.GroupID);
            string groupName = group.Name;

            if ((model.NumberTypeID == (int)NumberType.VPN && groupName != "VPN") || (model.NumberTypeID != (int)NumberType.VPN && groupName == "VPN"))
            {
                ModelState.AddModelError("NumberTypeID", "Tip broja ne pripada izabranoj grupi.");
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

                GroupNumberModel groupNumber = new GroupNumberModel()
                {
                    GroupID = model.GroupID,
                    Numbers = number,
                    InsertDate = DateTime.Now
                };
                db.GroupNumbers.Add(groupNumber);

                db.SaveChanges();

                return RedirectToAction("Numbers", "AdminManage", new { id = model.ClientID });
            }

            model.NumberType = group.Name == "VPN"
                ? new SelectList(db.NumberType.Where(nt => nt.Name == "VPN"), "NumberTypeID", "Name", 1)
                : new SelectList(db.NumberType.Where(nt => nt.Name != "VPN"), "NumberTypeID", "Name", 1);

            return View(model);
        }

        // GET: /AdminManage/EditNumber/{NumberID}
        public IActionResult EditNumber(int id)
        {
            var number = (from gnumbers in db.Numbers
                          where gnumbers.NumberID == id
                          select new AdminEditNumberViewModel()
                          {
                              ClientID = (int)gnumbers.Clients.ClientID,
                              NumberID = gnumbers.NumberID,
                              NumberTypeID = gnumbers.NumberType.NumberTypeID,
                              Number = gnumbers.Number,
                              Name = gnumbers.Name,
                              SendAllowed = gnumbers.SendAllowed
                          }).First();

            AdminEditNumberViewModel model = new AdminEditNumberViewModel
            {
                NumberType = new SelectList(db.NumberType, "NumberTypeID", "Name", 1),
                ClientID = number.ClientID,
                NumberID = number.NumberID,
                NumberTypeID = number.NumberTypeID,
                Number = number.Number,
                Name = number.Name,
                SendAllowed = number.SendAllowed
            };

            return View(model);
        }

        // POST: /AdminManage/EditNumber
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditNumber(AdminEditNumberViewModel model)
        {
            if (ModelState.IsValid)
            {
                var number = db.Numbers.Find(model.NumberID);
                number.ClientID = model.ClientID;
                number.NumberTypeID = model.NumberTypeID;
                number.Number = model.Number;
                number.Name = model.Name;
                number.SendAllowed = model.SendAllowed;
                number.InsertDate = DateTime.Now;

                db.SaveChanges();

                return RedirectToAction("Numbers", "AdminManage", new { id = model.ClientID });
            }

            model.NumberType = new SelectList(db.NumberType, "NumberTypeID", "Name", 1);
            return View(model);
        }
        #endregion

        #region Alphanumerics
        // GET: /AdminManage/Alphanumerics/{ClientID}
        public IActionResult Alphanumerics(int? id)
        {
            if (id == null)
                return BadRequest();

            ViewBag.Client = db.Client.Find(id).Name;
            List<AlphanumericModel> model = db.Alphanumeric.Where(a => a.ClientID == id).ToList();

            return View(model);
        }

        // GET: /AdminManage/EditAlphanumeric/{AlphanumericID}
        public IActionResult EditAlphanumeric(int id)
        {
            AlphanumericModel alphanumeric = db.Alphanumeric.Find(id);
            if (alphanumeric == null)
                return NotFound();

            AlphanumericViewModel model = new AlphanumericViewModel()
            {
                AlphanumericID = alphanumeric.AlphanumericID,
                Alphanumeric = alphanumeric.Alphanumeric,
                ClientID = alphanumeric.ClientID
            };

            return View(model);
        }

        // POST: /AdminManage/EditAlphanumeric
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditAlphanumeric(AlphanumericViewModel model)
        {
            if (ModelState.IsValid)
            {
                var alphanumeric = db.Alphanumeric.Find(model.AlphanumericID);
                alphanumeric.Alphanumeric = model.Alphanumeric;
                alphanumeric.ClientID = model.ClientID;
                alphanumeric.InsertDate = DateTime.Now;

                db.SaveChanges();

                return RedirectToAction("Alphanumerics", new { id = model.ClientID });
            }

            return View(model);
        }

        // GET: /AdminManage/CreateAlphanumeric/{ClientID}
        public IActionResult CreateAlphanumeric(int id)
        {
            AlphanumericCreateViewModel model = new AlphanumericCreateViewModel()
            {
                ClientID = id
            };

            return View(model);
        }

        // POST: /AdminManage/CreateAlphanumeric
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateAlphanumeric(AlphanumericCreateViewModel model)
        {
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
            }

            return RedirectToAction("Alphanumerics", new { id = model.ClientID });
        }
        #endregion

        #region MessageCost
        // GET: /AdminManage/MessageCost — aktivne cene grupisane po tipu broja
        public IActionResult MessageCost()
        {
            IEnumerable<MessageCostModel> costModel = db.MessageCost.ToList();

            var model = costModel.Where(cm => cm.EndDate == null)
                .OrderBy(cm => cm.NumberOfMessagesFrom)
                .OrderBy(cm => cm.NumberTypeID)
                .GroupBy(cm => cm.NumberTypeID)
                .Select(cm => new MessageCostListViewModel()
                {
                    NumberType = db.NumberType.Find(cm.Key).Name,
                    MessageCosts = cm
                });

            return View(model);
        }

        // GET: /AdminManage/EditMessageCost/{MessageCostID}
        public IActionResult EditMessageCost(int id)
        {
            var model = from mc in db.MessageCost
                        join nt in db.NumberType on mc.NumberTypeID equals nt.NumberTypeID
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

            if (!model.Any())
                return RedirectToAction("MessageCost");

            return View(model.FirstOrDefault());
        }

        // POST: /AdminManage/EditMessageCost — stara cena se zatvara (EndDate), upisuje se nova
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditMessageCost(EditMessageCostViewModel model)
        {
            if (ModelState.IsValid)
            {
                var messageCostOld = db.MessageCost.Find(model.MessageCostID);

                if (model.Equals(messageCostOld))
                {
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

                return RedirectToAction("MessageCost");
            }

            return View(model);
        }

        // GET: /AdminManage/CreateMessageCost
        public IActionResult CreateMessageCost()
        {
            CreateMessageCostViewModel model = new CreateMessageCostViewModel
            {
                NumberTypes = new SelectList(db.NumberType, "NumberTypeID", "Name", 1)
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateMessageCost(CreateMessageCostViewModel model)
        {
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

                return RedirectToAction("MessageCost");
            }

            model.NumberTypes = new SelectList(db.NumberType, "NumberTypeID", "Name", 1);
            return View(model);
        }
        #endregion

        #region FileDownload
        public IActionResult DownloadManual()
        {
            return PhysicalFile(Path.Combine(UploadedFilesPath, "KorisnickoUputstvoBizSMS_administrator.pdf"), "application/pdf");
        }
        #endregion
    }
}
