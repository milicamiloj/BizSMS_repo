using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace BizSMS.Web.Helpers
{
    // Facade sa istim call-site-ovima kao legacy log4net Logger. Faza 5 (§9.1):
    // interna implementacija piše red u SP dbo.Log_Insert (verno legacy AdoNetAppender-u),
    // uz paralelni upis u DI ILogger (konzola/dev). Isti 8 parametara kao legacy appender.
    public class Logger
    {
        private const string LogSource = "BizSMS.Web";

        private static ILoggerFactory _factory = LoggerFactory.Create(b => b.AddConsole());
        private static string? _connectionString;
        private static IHttpContextAccessor? _httpContextAccessor;

        private readonly ILogger _log;
        private string _controller = "";
        private string _action = "";

        // Pozvati posle builder.Build() u Program.cs. connectionString je "BizSMS";
        // ako je prazan ili sadrži __placeholder__ → SP upis se preskače (dev bez baze).
        public static void Configure(ILoggerFactory factory, string connectionString, IHttpContextAccessor httpContextAccessor)
        {
            _factory = factory;
            _connectionString = connectionString;
            _httpContextAccessor = httpContextAccessor;
        }

        public Logger() => _log = _factory.CreateLogger("BizSMS");

        public void SetControllerAction(string controller, string action)
        {
            _controller = controller;
            _action = action;
        }

        public void Info(string message)
        {
            _log.LogInformation("{Controller}/{Action} {Message}", _controller, _action, message);
            WriteToDb("INFO", message);
        }

        public void Warn(string message)
        {
            _log.LogWarning("{Controller}/{Action} {Message}", _controller, _action, message);
            WriteToDb("WARN", message);
        }

        public void Error(string message)
        {
            _log.LogError("{Controller}/{Action} {Message}", _controller, _action, message);
            WriteToDb("ERROR", message);
        }

        // Upis u SP dbo.Log_Insert. Nikad ne sme da obori zahtev — sve greške se gutaju
        // (kao log4net koji podrazumevano ignoriše greške appender-a).
        private void WriteToDb(string level, string message)
        {
            if (string.IsNullOrWhiteSpace(_connectionString) || _connectionString.Contains("__"))
                return;

            try
            {
                var user = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "";

                using var conn = new SqlConnection(_connectionString);
                using var cmd = new SqlCommand("dbo.Log_Insert", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.Add("@log_date", SqlDbType.DateTime).Value = DateTime.Now;
                cmd.Parameters.Add("@log_level", SqlDbType.VarChar, 50).Value = level;
                cmd.Parameters.Add("@log_source", SqlDbType.VarChar, 300).Value = LogSource;
                cmd.Parameters.Add("@user", SqlDbType.VarChar, 50).Value = Truncate(user, 50);
                cmd.Parameters.Add("@controller", SqlDbType.VarChar, 100).Value = Truncate(_controller, 100);
                cmd.Parameters.Add("@action", SqlDbType.VarChar, 100).Value = Truncate(_action, 100);
                cmd.Parameters.Add("@log_message", SqlDbType.VarChar, 4000).Value = Truncate(message ?? "", 4000);
                cmd.Parameters.Add("@exception", SqlDbType.VarChar, 4000).Value = DBNull.Value;

                conn.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Upis u dbo.Log_Insert nije uspeo");
            }
        }

        private static string Truncate(string value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);
    }
}
