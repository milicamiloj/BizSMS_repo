using BizSMS.Poc.Net10.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BizSMS.Poc.Net10.Data;

public sealed class BizSmsDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public BizSmsDbContext(DbContextOptions<BizSmsDbContext> options) : base(options) { }

    public DbSet<ClientModel> Clients => Set<ClientModel>();
    public DbSet<NumbersModel> Numbers => Set<NumbersModel>();
    public DbSet<MessageModel> Messages => Set<MessageModel>();
    public DbSet<ScheduledSmsModel> ScheduledSms => Set<ScheduledSmsModel>();
    public DbSet<LogModel> Logs => Set<LogModel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ClientModel>(b =>
        {
            b.ToTable("BST_CLIENTS");
            b.HasKey(x => x.ClientID);
            b.Property(x => x.ClientID).HasColumnName("Client_ID");
            b.Property(x => x.Name).HasMaxLength(200).IsRequired();
            b.Property(x => x.MtsID).HasColumnName("MTS_ID").HasMaxLength(15).IsRequired();
            b.Property(x => x.PhoneNumber).HasColumnName("Phone_Number").HasMaxLength(13);
            b.Property(x => x.IsCanceled).HasColumnName("Is_Canceled");
            b.Property(x => x.InsertDate).HasColumnName("Insert_Date");
        });

        builder.Entity<NumbersModel>(b =>
        {
            b.ToTable("BST_NUMBERS");
            b.HasKey(x => x.NumberID);
            b.Property(x => x.NumberID).HasColumnName("Number_ID");
            b.Property(x => x.Number).HasMaxLength(12).IsRequired();
            b.Property(x => x.Name).HasMaxLength(30);
            b.Property(x => x.NumberTypeID).HasColumnName("Number_Type_ID");
            b.Property(x => x.ClientID).HasColumnName("Client_ID");
            b.Property(x => x.ContractId).HasColumnName("Contract_ID").HasMaxLength(50);
            b.Property(x => x.SendAllowed).HasColumnName("Send_allowed");
            b.Property(x => x.Active);
            b.Property(x => x.InsertDate).HasColumnName("Insert_Date");
        });

        builder.Entity<MessageModel>(b =>
        {
            b.ToTable("BST_MESSAGES");
            b.HasKey(x => x.MessageID);
            b.Property(x => x.MessageID).HasColumnName("Message_ID");
            b.Property(x => x.Sender).HasMaxLength(13).IsRequired();
            b.Property(x => x.MessageText).HasColumnName("Message_Text").HasMaxLength(765).IsRequired();
            b.Property(x => x.MessageLength).HasColumnName("Message_Length");
            b.Property(x => x.SendDate).HasColumnName("Send_Date");
            b.Property(x => x.InsertDate).HasColumnName("Insert_Date");
            b.Property(x => x.UserID).HasColumnName("User_ID");
        });

        builder.Entity<ScheduledSmsModel>(b =>
        {
            b.ToTable("BST_SCHEDULED_SMS");
            b.HasKey(x => new { x.HangfireID, x.MessageID });
            b.Property(x => x.HangfireID).HasColumnName("Hangfire_ID").HasMaxLength(128);
            b.Property(x => x.MessageID).HasColumnName("Message_ID");
            b.Property(x => x.UserInsert).HasColumnName("User_Insert").IsRequired();
            b.Property(x => x.InsertDate).HasColumnName("Insert_Date");
            b.Property(x => x.CancelDate).HasColumnName("Cancel_Date");
            b.Property(x => x.UserID).HasColumnName("User_Cancel");
        });

        builder.Entity<LogModel>(b =>
        {
            b.ToTable("BST_LOG");
            b.HasKey(x => x.LogID);
            b.Property(x => x.LogID).HasColumnName("Log_ID");
            b.Property(x => x.LogDate).HasColumnName("Log_Date");
            b.Property(x => x.LogLevel).HasColumnName("Log_Level").HasMaxLength(50);
            b.Property(x => x.LogSource).HasColumnName("Log_Source").HasMaxLength(50);
            b.Property(x => x.User).HasMaxLength(50);
            b.Property(x => x.Controller).HasMaxLength(100);
            b.Property(x => x.Action).HasMaxLength(100);
            b.Property(x => x.LogMessage).HasColumnName("Log_Message").HasMaxLength(4000);
            b.Property(x => x.Exception).HasMaxLength(4000);
        });
    }
}
