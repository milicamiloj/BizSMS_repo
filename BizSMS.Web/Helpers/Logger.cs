using Microsoft.Extensions.Logging;

namespace BizSMS.Web.Helpers
{
    // Privremeni facade koji zadržava iste call-site-ove kao legacy log4net Logger.
    // Faza 5 (§9.1) menja internu implementaciju na log4net -> SP Log_Insert.
    public class Logger
    {
        private static ILoggerFactory _factory = LoggerFactory.Create(b => b.AddConsole());
        private readonly ILogger _log;
        private string _controller = "";
        private string _action = "";

        public static void Configure(ILoggerFactory factory) => _factory = factory;

        public Logger() => _log = _factory.CreateLogger("BizSMS");

        public void SetControllerAction(string controller, string action)
        {
            _controller = controller;
            _action = action;
        }

        public void Info(string message) =>
            _log.LogInformation("{Controller}/{Action} {Message}", _controller, _action, message);

        public void Warn(string message) =>
            _log.LogWarning("{Controller}/{Action} {Message}", _controller, _action, message);

        public void Error(string message) =>
            _log.LogError("{Controller}/{Action} {Message}", _controller, _action, message);
    }
}
