using System;
using System.Runtime.Serialization;

namespace UnsubBizSMSWebService.Exceptions
{
    [Serializable]
    internal class UserInVpnGroupException : Exception
    {
        public UserInVpnGroupException()
        {
        }

        public UserInVpnGroupException(string message) : base(message)
        {
        }

        public UserInVpnGroupException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected UserInVpnGroupException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}