namespace BizSMS.Poc.Net10.Models;

public sealed class LogModel
{
    public int LogID { get; set; }
    public DateTime LogDate { get; set; }
    public string LogLevel { get; set; } = "INFO";
    public string LogSource { get; set; } = string.Empty;
    public string? User { get; set; }
    public string? Controller { get; set; }
    public string? Action { get; set; }
    public string? LogMessage { get; set; }
    public string? Exception { get; set; }
}
