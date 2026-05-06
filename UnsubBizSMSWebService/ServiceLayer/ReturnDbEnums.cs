using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace UnsubBizSMSWebService.ServiceLayer
{
    public enum ReturnDbEnums
    {
        DatabaseProcessFailure = -1,
        SuccessUnsub = 0,
        UserAlreadyUnsub = 1,
        Number_or_AlphanumNonExisting = 2,
        UserInVpnGroup = 3,
        Number_or_CLientID_NonExisting = 4
    }
}