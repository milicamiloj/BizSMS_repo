using BizSMS.Poc.Net10.Models;
using BizSMS.Poc.Net10.Models.ViewModels;
using BizSMS.Poc.Net10.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Poc.Net10.Controllers;

[Authorize(Roles = "Administrator,BusinessUser")]
public sealed class SmsFlowController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IActionOtpService _actionOtpService;
    private readonly ISmsWorkflowService _smsWorkflowService;

    public SmsFlowController(
        UserManager<ApplicationUser> userManager,
        IActionOtpService actionOtpService,
        ISmsWorkflowService smsWorkflowService)
    {
        _userManager = userManager;
        _actionOtpService = actionOtpService;
        _smsWorkflowService = smsWorkflowService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "PoC Send/Schedule";
        return View(new RequestSendOtpViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestOtp(RequestSendOtpViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(nameof(Index), model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var command = new PendingSendCommand
        {
            ScopeId = Guid.NewGuid().ToString("N"),
            NumberId = model.NumberId,
            MessageText = model.MessageText,
            ScheduledAtUtc = model.ScheduledAtUtc
        };

        await _actionOtpService.RequestOtpForCommandAsync(user, command, ct);

        return View("ConfirmOtp", new ConfirmSendOtpViewModel
        {
            ScopeId = command.ScopeId
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmOtp(ConfirmSendOtpViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View("ConfirmOtp", model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var command = await _actionOtpService.ConfirmAndConsumeCommandAsync(user, model.ScopeId, model.OtpCode, ct);
        if (command is null)
        {
            ModelState.AddModelError(string.Empty, "Neispravan OTP ili je sesija potvrde istekla.");
            return View("ConfirmOtp", model);
        }

        var result = await _smsWorkflowService.ExecuteAsync(user, command, ct);
        return View("Result", result);
    }
}
