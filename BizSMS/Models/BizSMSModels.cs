using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace BizSMS.Models
{
    public class BizSMSModels
    {
    }

    [Table("BST_GROUPS")]
    public class GroupModel
    {
        [Key]
        [Column("Group_ID")]
        public int GroupID { get; set; }

        [Required]
        [StringLength(30)]
        public string Name { get; set; }

        public bool Default { get; set; }

        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        //Foreign key
        [Column("Client_ID")]
        public int ClientID { get; set; }
        public virtual ClientModel Client { get; set; }

        //dodao kad pravljenja metode SendGroupSMS
        public virtual ICollection<GroupNumberModel> GroupNumbers { get; set; }
    }

    [Table("BST_CLIENTS")]
    public class ClientModel
    {
        [Key]
        [Column("Client_ID")]
        public int ClientID { get; set; }

        //[Required]
        //[Column("Contract_ID")]
        //[StringLength(50)]
        //public string ContractID { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; }

        [Column("MTS_ID")]
        [Required]
        [StringLength(15)]
        public string MtsID { get; set; }

        [Column("Phone_Number")]
        [StringLength(13)]
        public string PhoneNumber { get; set; }

        [Column("Is_Canceled")]
        public bool IsCanceled { get; set; }

        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        public virtual ICollection<ApplicationUser> ApplicationUsers { get; set; }
        public virtual ICollection<AlphanumericModel> Alphanumeric { get; set; }
        public virtual ICollection<GroupModel> Groups { get; set; }
        public virtual ICollection<NumbersModel> Numbers { get; set; }
        public virtual ICollection<ClientContractsModel> Contracts { get; set; }
    }

    [Table("BST_MESSAGES")]
    public class MessageModel
    {
        [Key]
        [Column("Message_ID")]
        public int MessageID { get; set; }

        [Required]
        [StringLength(13)]
        public string Sender { get; set; }

        [Required]
        [Column("Message_Text")]
        [StringLength(765)]
        public string MessageText { get; set; }

        [Column("Message_Length")]
        public int MessageLength { get; set; }

        [Column("Send_Date")]
        public DateTime SendDate { get; set; }

        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        public bool Test { get; set; }

        public int Status { get; set; }

        public bool Charged { get; set; }

        [Column("User_ID")]
        public virtual string UserID { get; set; }
        public virtual ApplicationUser User { get; set; }

        public virtual ICollection<MessageNumberModel> MessagesNumbers { get; set; }
        public virtual ICollection<ScheduledSmsModel> ScheduledSms
        { get; set; }
    }



    [Table("BST_NUMBERS")]
    public class NumbersModel
    {
        [Key]
        [Column("Number_ID")]
        public int NumberID { get; set; }

        [Index]
        [Required]
        [StringLength(12)]
        public string Number { get; set; }

        [StringLength(30)]
        public string Name { get; set; }

        [Column("Send_allowed")]
        public bool SendAllowed { get; set; }

        [Column("Check_Date")]
        public DateTime? CheckDate { get; set; }

        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        public bool Active { get; set; }

        [Column("Contract_ID")]
        [StringLength(50)]
        public string ContractId { get; set; }

        //Foreign Keys

        [Column("Client_ID")]
        public virtual int? ClientID { get; set; }
        public virtual ClientModel Clients { get; set; }

        [Column("Number_Type_ID")]
        public virtual int NumberTypeID { get; set; }
        public virtual NumberTypeModel NumberType { get; set; }

        public virtual ICollection<MessageNumberModel> MessagesNumbers { get; set; }

        public virtual ICollection<GroupNumberModel> GroupNumbers { get; set; }

        public virtual ICollection<DenySendingReasonModel> DenySendingReasons { get; set; }



    }

    [Table("BST_GROUP_NUMBER")]
    public class GroupNumberModel
    {
        [Key]
        [Column("Group_ID", Order = 1)]
        public int GroupID { get; set; }

        [Key]
        [Column("Number_ID", Order = 2)]
        public int NumberID { get; set; }

        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        public virtual GroupModel Groups { get; set; }
        public virtual NumbersModel Numbers { get; set; }
    }

    [Table("BST_MESSAGE_NUMBER")]
    public class MessageNumberModel
    {
        [Key]
        [Column("Number_ID", Order = 1)]
        public int NumberID { get; set; }

        [Column("Send_Date")]
        public DateTime SendDate { get; set; }

        [StringLength(50)]
        public string SendSMSID { get; set; }

        public bool Sent { get; set; }

        public int Delivered { get; set; }

        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        [Column("Delivery_Date")]
        public DateTime? DeliveryDate { get; set; }

        public bool Charged { get; set; }

        [Column("Message_Length_NT")]
        public int MessageLengthNT { get; set; }

        //Foreign keys
        public virtual NumbersModel NumbersModel { get; set; }

        [Key]
        [Column("Message_ID", Order = 2)]
        public virtual int MessageID { get; set; }
        public virtual MessageModel Message { get; set; }

        public virtual int? NumberTypeID { get; set; }
        public virtual NumberTypeModel NumberType { get; set; }
    }

    [Table("BSL_NUMBER_TYPE")]
    public class NumberTypeModel
    {
        [Key]
        [Column("Number_Type_ID")]
        public int NumberTypeID { get; set; }

        [Required]
        [StringLength(10)]
        public string Name { get; set; }

        public virtual ICollection<MessageCostModel> MessageCost { get; set; }

        public virtual ICollection<MessageNumberModel> MessageNumber { get; set; }
    }

    [Table("BSL_MESSAGE_COST")]
    public class MessageCostModel
    {
        [Key]
        [Column("Message_Cost_ID")]
        public int MessageCostID { get; set; }

        [Column("Number_Of_Messages_From")]
        public int NumberOfMessagesFrom { get; set; }

        [Column("Number_Of_Messages_To")]
        public int NumberOfMessagesTo { get; set; }

        public double Price { get; set; }

        [Column("Start_Date")]
        public DateTime StartDate { get; set; }

        [Column("End_Date")]
        public DateTime? EndDate { get; set; }

        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        //Foreign key
        [Column("Number_Type_ID")]
        public virtual int NumberTypeID { get; set; }
        public virtual NumberTypeModel NumberType { get; set; }
    }
    
    [Table("BSL_ALPHANUMERIC")]
    public class AlphanumericModel
    {
        [Key]
        [Column("Alphanumeric_ID")]
        public int AlphanumericID { get; set; }

        [StringLength(11)]
        public string Alphanumeric { get; set; }

        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        [Column("Client_ID")]
        public virtual int ClientID { get; set; }
        public virtual ClientModel Client { get; set; }
    }


    [Table("BST_LOG")]
    public class Log
    {
        [Key]
        [Column("Log_ID")]
        public int LogID { get; set; }

        [Column("Log_Date")]
        public DateTime LogDate { get; set; }

        [Column("Log_Level")]
        [StringLength(50)]
        public string LogLevel { get; set; }

        [Column("Log_Source")]
        [StringLength(50)]
        public string LogSource { get; set; }

        [StringLength(50)]
        public string User { get; set; }

        [StringLength(100)]
        public string Controller { get; set; }

        [StringLength(100)]
        public string Action { get; set; }

        [Column("Log_Message")]
        [StringLength(4000)]
        public string LogMessage { get; set; }

        [StringLength(4000)]
        public string Exception { get; set; }
    }

    [Table("BST_TEMP_IMPORT")]
    public class TempImport
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public int GroupId { get; set; }
        public string Number { get; set; }
        public string Name { get; set; }
        public Helpers.NumberType NumberType { get; set; }
    }

    [Table("BST_CLIENT_CONTRACTS")]
    public class ClientContractsModel
    {
        [Key]
        [Column("Client_Contracts_ID")]
        public int ClientContractsId { get; set; }

        [Required]
        [Column("Contract_ID")]
        [StringLength(50)]
        public string ContractId { get; set; }

        [Column("Synchronization_Date")]
        public DateTime SynchronizationDate { get; set; }

        [Column("Client_ID")]
        public int ClientId { get; set; }

        public virtual ClientModel Client { get; set; }
    }

    [Table("BST_DENY_SENDING_REASON")]
    public class DenySendingReasonModel
    {
        [Key]
        [Column("Deny_Reason_ID")]
        public int DenyReasonID { get; set; }

        [Required]
        [StringLength(255)]
        public string Reason { get; set; }

        [Required]
        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        [Column("Number_ID")]
        public int NumberID { get; set; }

        [Column("Send_allowed")]
        public bool SendAllowed { get; set; }

        [Column("User_ID")]
        public virtual string UserID { get; set; }
        public virtual ApplicationUser User { get; set; }
    }

    [Table("BST_SCHEDULED_SMS")]
    public class ScheduledSmsModel
    {
        [Key]
        [Column("Hangfire_ID", Order = 0)]
        public string HangfireID { get; set; }

        [Required]
        [Column("User_Insert")]
        public string UserInsert { get; set; }

        [Required]
        [Column("Insert_Date")]
        public DateTime InsertDate { get; set; }

        [Column("Cancel_Date")]
        public DateTime? CancelDate { get; set; }

        //foreign key
        [Key]
        [Column("Message_ID", Order = 1)]
        public virtual int MessageID { get; set; }

        [Column("User_Cancel", Order = 3)]
        public virtual string UserID { get; set; }
        public virtual ApplicationUser User { get; set; }
    }
}