#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace BizSMS.Web.Models
{
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
    }
}
