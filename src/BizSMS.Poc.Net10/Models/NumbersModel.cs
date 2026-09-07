namespace BizSMS.Poc.Net10.Models;

public sealed class NumbersModel
{
    public int NumberID { get; set; }
    public string Number { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool SendAllowed { get; set; }
    public bool Active { get; set; }
    public DateTime InsertDate { get; set; }
    public string? ContractId { get; set; }
    public int? ClientID { get; set; }
    public int NumberTypeID { get; set; }
}
