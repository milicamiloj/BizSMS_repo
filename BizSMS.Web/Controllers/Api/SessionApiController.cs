using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Web.Controllers.Api
{
    // Zamena za legacy BizSMS.Controllers.API.SessionController.
    // Provera da li je sesija još aktivna (neautentifikovan -> 401 preko cookie eventa).
    [ApiController]
    [Authorize]
    public class SessionApiController : ControllerBase
    {
        [HttpGet("api/session/check")]
        public IActionResult CheckSession()
        {
            return Ok();
        }
    }
}
