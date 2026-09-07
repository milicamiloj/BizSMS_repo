using System.ComponentModel.DataAnnotations;

namespace BizSMS.Poc.Net10.Models.ViewModels;

public sealed class Verify2FaViewModel
{
    [Required]
    public string Code { get; set; } = string.Empty;
}
