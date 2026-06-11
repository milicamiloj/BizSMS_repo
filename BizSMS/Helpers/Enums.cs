using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace BizSMS.Helpers
{
    //public static class NumberType
    //{
    //    public static string VPN { get { return "VPN"; } }
    //    public static string U_MTS { get { return "U MTS"; } }
    //    public static string VAN_MTS { get { return "VAN MTS"; } }
    //}

    public enum NumberType
    {
        VPN = 1,
        U_MTS = 2,
        VAN_MTS = 3
    }

    public enum MessageStatus
    {
        Queued = 1,
        Scheduled = 2,
        Processing = 3,
        Finished = 4,
        ScheduledSendingCanceled = 5
    }

    public enum ManageMessageId
    {
        AddPhoneSuccess,
        ChangePasswordSuccess,
        SetTwoFactorSuccess,
        SetPasswordSuccess,
        RemoveLoginSuccess,
        RemovePhoneSuccess,
        Error
    }
}