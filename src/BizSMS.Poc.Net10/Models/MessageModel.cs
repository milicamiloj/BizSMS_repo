namespace BizSMS.Poc.Net10.Models;

public sealed class MessageModel
{
    public int MessageID { get; set; }
    public string Sender { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public int MessageLength { get; set; }
    public DateTime SendDate { get; set; }
    public DateTime InsertDate { get; set; }
    public bool Test { get; set; }
    public int Status { get; set; }
    public bool Charged { get; set; }
    public string UserID { get; set; } = string.Empty;
}
