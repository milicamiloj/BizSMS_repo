namespace BizSMS.Migrations
{
    using Microsoft.AspNet.Identity.EntityFramework;
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Migrations;
    using System.Linq;
    using BizSMS.Models;
    using Microsoft.AspNet.Identity;

    internal sealed class Configuration : DbMigrationsConfiguration<BizSMS.Models.ApplicationDbContext>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
            ContextKey = "BizSMS.Models.ApplicationDbContext";
        }

        protected override void Seed(BizSMS.Models.ApplicationDbContext context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method 
            //  to avoid creating duplicate seed data. E.g.
            //
            //    context.People.AddOrUpdate(
            //      p => p.FullName,
            //      new Person { FullName = "Andrew Peters" },
            //      new Person { FullName = "Brice Lambson" },
            //      new Person { FullName = "Rowan Miller" }
            //    );
            //
           
            var userStore = new UserStore<ApplicationUser>(context);
            var userManager = new UserManager<ApplicationUser>(userStore);

            var roleStore = new RoleStore<IdentityRole>(context);
            var roleManager = new RoleManager<IdentityRole>(roleStore);

            const string name = "telekom";
            const string email = "telekom@telekom.rs";
            const string password = "Asdqwe!23";
            string[] roleNames = { "Administrator", "Client", "User" };
            string[] numberTypes = { "VPN", "U MTS", "VAN MTS" };

            //Create client
            context.Client.AddOrUpdate(new Models.ClientModel()
            {
                ClientID = 5,
                Name = "Telekom",
                MtsID = "1234",
                //ContractID = "1234",
                PhoneNumber = "0646503707",
                InsertDate = DateTime.Now
            });

            context.SaveChanges();
            var client = context.Client.FirstOrDefault(p => p.Name == "Telekom");

            //Roles list
            foreach(var roleName in roleNames)
            {
                var role = roleManager.FindByName(roleName);
                if (role == null)
                {
                    role = new IdentityRole(roleName);
                    var roleresult = roleManager.Create(role);
                }
            }

            //User
            var user = userManager.FindByName(name);
            if (user == null)
            {
                user = new ApplicationUser { UserName = name, Email = email, ClientID = client.ClientID };
                var result = userManager.Create(user, password);
                result = userManager.SetLockoutEnabled(user.Id, true);
            }

            var rolesForUser = userManager.GetRoles(user.Id);
            if (!rolesForUser.Contains("Administrator"))
            {
                var result = userManager.AddToRole(user.Id, "Administrator");
            }

            //NumberTypes
            foreach (var numberType in numberTypes)
            {
                NumberTypeModel NT = new NumberTypeModel()
                {
                    Name = numberType
                };

                context.NumberType.AddOrUpdate(p=> p.Name, NT);
            }

            //Message Cost
                //VPN
            context.MessageCost.AddOrUpdate(AddMessageCost(1, 1, 100000, 0.45, 1));
            context.MessageCost.AddOrUpdate(AddMessageCost(2, 100001, 200000, 0.4, 1));
            context.MessageCost.AddOrUpdate(AddMessageCost(3, 200001, 300000, 0.35, 1));
            context.MessageCost.AddOrUpdate(AddMessageCost(4, 300001, 400000, 0.3, 1));

                //MTS Van VPN
            context.MessageCost.AddOrUpdate(AddMessageCost(5, 1, 50000, 1.3, 2));
            context.MessageCost.AddOrUpdate(AddMessageCost(6, 50001, 200000, 1.1, 2));
            context.MessageCost.AddOrUpdate(AddMessageCost(7, 200001, 600000, 0.8, 2));
            context.MessageCost.AddOrUpdate(AddMessageCost(8, 600001, 1000000, 0.6, 2));

                //MTS Van VPN
            context.MessageCost.AddOrUpdate(AddMessageCost(9, 1, 200000, 2.15, 3));
            context.MessageCost.AddOrUpdate(AddMessageCost(10, 200001, 600000, 2.1, 3));
            context.MessageCost.AddOrUpdate(AddMessageCost(11, 600001, 1000000, 2.05, 3));

            context.SaveChanges();
        }

        private MessageCostModel AddMessageCost(int mgId, int from, int to, double cost, int numberTypeId)
        {
            MessageCostModel mcm = new MessageCostModel()
            {
                MessageCostID = mgId,
                NumberOfMessagesFrom = from,
                NumberOfMessagesTo = to,
                NumberTypeID = numberTypeId,
                Price = cost,
                StartDate = new DateTime(2016, 1, 1),
                InsertDate = DateTime.Now
            };

            return mcm;
        }
    }
}
