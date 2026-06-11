using BizSMS.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;

namespace BizSMS.Controllers.API
{

    public class SessionController : ApiController
    {
        [AuthorizeApiUser]
        [Route("api/session/check")]
        [HttpGet]
        public IHttpActionResult CheckSession()
        {
            return Ok();
        }
       
    }
}