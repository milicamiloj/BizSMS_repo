using System.ComponentModel.DataAnnotations;

namespace BizSMS.Poc.Net10.Models.ViewModels;

public sealed class ConfirmSendOtpViewModel
{
    [Required]
    public string ScopeId { get; set; } = string.Empty;

    [Required]
    public string OtpCode { get; set; } = string.Empty;
}
