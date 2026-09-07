namespace BizSMS.Poc.Net10.Models;

public sealed class ClientModel
{
    public int ClientID { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MtsID { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public bool IsCanceled { get; set; }
    public DateTime InsertDate { get; set; }
}
