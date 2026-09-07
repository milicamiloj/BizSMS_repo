using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace BizSMS.Poc.Net10.Models;

public sealed class ApplicationUser : IdentityUser
{
    [Column("Is_Canceled")]
    public bool IsCanceled { get; set; }

    [Column("Is_Deleted")]
    public bool IsDeleted { get; set; }

    [Column("Client_ID")]
    public int ClientID { get; set; }

    [Column("PhoneCodeSentAt")]
    public DateTime? PhoneCodeSentAt { get; set; }
}
