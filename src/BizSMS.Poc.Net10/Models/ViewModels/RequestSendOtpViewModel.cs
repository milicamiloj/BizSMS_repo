using System.ComponentModel.DataAnnotations;

namespace BizSMS.Poc.Net10.Models.ViewModels;

public sealed class RequestSendOtpViewModel
{
    [Required]
    public int NumberId { get; set; }

    [Required]
    [StringLength(765)]
    public string MessageText { get; set; } = string.Empty;

    public DateTime? ScheduledAtUtc { get; set; }
}
