using System.Data.Entity;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BizSMS.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        [Column("Is_Canceled")]
        public bool IsCanceled { get; set; }

        [Column("Is_Deleted")]
        public bool IsDeleted { get; set; }

        [Column("Client_ID")]
        public virtual int ClientID { get; set; }

        [Column("PhoneCodeSentAt")]
        public DateTime? PhoneCodeSentAt { get; set; }
        public virtual ClientModel Client { get; set; }

        public virtual ICollection<MessageModel> Messages { get; set; }

        public virtual ICollection<DenySendingReasonModel> DenySendingReasons { get; set; }

        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext()
            : base("BIZSMS", throwIfV1Schema: false)
        {
            Database.SetInitializer<ApplicationDbContext>(null);
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }

        public DbSet<GroupModel> Group { get; set; }
        public DbSet<ClientModel> Client { get; set; }
        public DbSet<MessageModel> Message { get; set; }
        public DbSet<NumbersModel> Numbers{ get; set; }
        public DbSet<MessageNumberModel> MessagesNumbers { get; set; }
        public DbSet<MessageCostModel> MessageCost { get; set; }
        public DbSet<NumberTypeModel> NumberType { get; set; }
        public DbSet<AlphanumericModel> Alphanumeric { get; set; }
        public DbSet<GroupNumberModel> GroupNumbers { get; set; }
        public DbSet<Log> Logs { get; set; }
        public DbSet<TempImport> TempImport { get; set; }
        public DbSet<ClientContractsModel> ClientContract { get; set; }
        public DbSet<DenySendingReasonModel> DenySendingReason { get; set; }
        public DbSet<ScheduledSmsModel> ScheduledSms { get; set; }


        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Log>()
                .MapToStoredProcedures();

            //modelBuilder.Entity<ApplicationUser>().ToTable("BST_USERS");
            //modelBuilder.Entity<IdentityRole>().ToTable("BSL_ROLES");
            //modelBuilder.Entity<IdentityUserRole>().ToTable("BST_USER_ROLES");

        }

        object placeHolderVariable;
    }
}