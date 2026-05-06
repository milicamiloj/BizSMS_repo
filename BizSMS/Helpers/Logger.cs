using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BizSMS.Helpers
{
    public class Logger
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        public void SetControllerAction(string controller, string action)
        {
            log4net.GlobalContext.Properties["Controller"] = controller;
            log4net.GlobalContext.Properties["Action"] = action;
        }
        
        public void Error(string message)
        {
            log.Error(message);
        }

        public void Info(string message)
        {
            log.Info(message);
        }

        public void Warn(string message)
        {
            log.Warn(message);
        }
    }
}