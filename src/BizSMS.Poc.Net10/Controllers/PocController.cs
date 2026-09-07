using BizSMS.Poc.Net10.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Poc.Net10.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class PocController : Controller
{
    private readonly IPocDbVerificationService _verificationService;

    public PocController(IPocDbVerificationService verificationService)
    {
        _verificationService = verificationService;
    }

    [HttpPost]
    public async Task<IActionResult> VerifyDb(CancellationToken ct)
    {
        var result = await _verificationService.VerifyReadWriteAsync(ct);
        return Json(result);
    }
}
