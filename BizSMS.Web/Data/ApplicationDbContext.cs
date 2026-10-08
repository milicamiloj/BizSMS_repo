using BizSMS.Web.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Web.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<GroupModel> Group { get; set; } = null!;
        public DbSet<ClientModel> Client { get; set; } = null!;
        public DbSet<MessageModel> Message { get; set; } = null!;
        public DbSet<NumbersModel> Numbers { get; set; } = null!;
        public DbSet<MessageNumberModel> MessagesNumbers { get; set; } = null!;
        public DbSet<MessageCostModel> MessageCost { get; set; } = null!;
        public DbSet<NumberTypeModel> NumberType { get; set; } = null!;
        public DbSet<AlphanumericModel> Alphanumeric { get; set; } = null!;
        public DbSet<GroupNumberModel> GroupNumbers { get; set; } = null!;
        public DbSet<Log> Logs { get; set; } = null!;
        public DbSet<TempImport> TempImport { get; set; } = null!;
        public DbSet<ClientContractsModel> ClientContract { get; set; } = null!;
        public DbSet<DenySendingReasonModel> DenySendingReason { get; set; } = null!;
        public DbSet<ScheduledSmsModel> ScheduledSms { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Kompozitni ključevi (EF Core ignoriše [Column(Order)] iz EF6 anotacija)
            modelBuilder.Entity<GroupNumberModel>()
                .HasKey(gn => new { gn.GroupID, gn.NumberID });

            // FK mapiranje junction tabele na POSTOJEĆE kolone (bez ovoga EF Core pravi
            // shadow FK "NumbersNumberID"/"GroupsGroupID" i traži nepostojeće kolone).
            modelBuilder.Entity<GroupNumberModel>()
                .HasOne(gn => gn.Groups)
                .WithMany(g => g.GroupNumbers)
                .HasForeignKey(gn => gn.GroupID);

            modelBuilder.Entity<GroupNumberModel>()
                .HasOne(gn => gn.Numbers)
                .WithMany(n => n.GroupNumbers)
                .HasForeignKey(gn => gn.NumberID);

            modelBuilder.Entity<MessageNumberModel>()
                .HasKey(mn => new { mn.NumberID, mn.MessageID });

            // FK mapiranje junction tabele BST_MESSAGE_NUMBER na postojeće kolone.
            // Bez ovoga EF Core pravi shadow FK "NumbersModelNumberID" (nav se zove NumbersModel,
            // a FK kolona je Number_ID) i report upiti pucaju ("Invalid column name").
            modelBuilder.Entity<MessageNumberModel>()
                .HasOne(mn => mn.NumbersModel)
                .WithMany(n => n.MessagesNumbers)
                .HasForeignKey(mn => mn.NumberID);

            modelBuilder.Entity<MessageNumberModel>()
                .HasOne(mn => mn.Message)
                .WithMany(m => m.MessagesNumbers)
                .HasForeignKey(mn => mn.MessageID);

            modelBuilder.Entity<MessageNumberModel>()
                .HasOne(mn => mn.NumberType)
                .WithMany(nt => nt.MessageNumber)
                .HasForeignKey(mn => mn.NumberTypeID);

            // MessageModel.User (BST_MESSAGES.User_ID -> AspNetUsers) i ScheduledSms
            modelBuilder.Entity<MessageModel>()
                .HasOne(m => m.User)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.UserID);

            modelBuilder.Entity<ScheduledSmsModel>()
                .HasKey(s => new { s.HangfireID, s.MessageID });

            modelBuilder.Entity<MessageModel>()
                .HasMany(m => m.ScheduledSms)
                .WithOne()
                .HasForeignKey(s => s.MessageID);

            modelBuilder.Entity<ScheduledSmsModel>()
                .HasOne(s => s.User)
                .WithMany()
                .HasForeignKey(s => s.UserID);

            // NumbersModel.Clients -> BST_NUMBERS.Client_ID -> BST_CLIENTS. Navigacija se zove
            // "Clients" (množina), pa EF Core po konvenciji traži shadow FK "ClientsClientID" i
            // puca ("Invalid column name 'ClientsClientID'"); eksplicitno vežemo za ClientID.
            modelBuilder.Entity<NumbersModel>()
                .HasOne(n => n.Clients)
                .WithMany(c => c.Numbers)
                .HasForeignKey(n => n.ClientID);

            // NumbersModel.DenySendingReasons -> BST_DENY_SENDING_REASON.Number_ID. Kolekcija nema
            // inverznu navigaciju na DenySendingReasonModel, pa EF Core po konvenciji ne prepoznaje
            // postojeći NumberID kao FK već traži shadow FK "NumbersModelNumberID" (upit lazy load-a
            // puca: "Invalid column name 'NumbersModelNumberID'"); eksplicitno vežemo za NumberID.
            modelBuilder.Entity<NumbersModel>()
                .HasMany(n => n.DenySendingReasons)
                .WithOne()
                .HasForeignKey(d => d.NumberID);

            // Indeks na Number (bio [Index] u EF6)
            modelBuilder.Entity<NumbersModel>()
                .HasIndex(n => n.Number);
        }
    }
}
