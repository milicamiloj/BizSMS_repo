namespace BizSMS.Web.Helpers
{
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
