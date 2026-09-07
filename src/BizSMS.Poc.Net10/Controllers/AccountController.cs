using BizSMS.Poc.Net10.Models;
using BizSMS.Poc.Net10.Models.ViewModels;
using BizSMS.Poc.Net10.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace BizSMS.Poc.Net10.Controllers;

public sealed class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IOtpSender _otpSender;
    private readonly IAuditService _audit;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IOtpSender otpSender,
        IAuditService audit)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _otpSender = otpSender;
        _audit = audit;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByNameAsync(model.Username);
        if (user is null || user.IsCanceled || user.IsDeleted)
        {
            ModelState.AddModelError(string.Empty, "Neispravno korisničko ime ili lozinka.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            model.Username, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.RequiresTwoFactor)
        {
            var code = await _userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultPhoneProvider);
            user.PhoneCodeSentAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await _otpSender.SendAsync(user.PhoneNumber ?? string.Empty, code, "LOGIN_2FA");
            await _audit.LogAsync("LOGIN_OTP_SENT", "Login 2FA code sent", user.UserName);
            return RedirectToAction(nameof(Verify2Fa), new { returnUrl });
        }

        if (result.Succeeded)
        {
            await _audit.LogAsync("LOGIN_SUCCESS", "Login success without 2FA challenge", user.UserName);
            return LocalRedirect(returnUrl ?? Url.Action("Index", "SmsFlow")!);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Nalog je zaključan.");
            return View(model);
        }

        await _audit.LogAsync("LOGIN_FAILED", "Login failed", model.Username);
        ModelState.AddModelError(string.Empty, "Neispravno korisničko ime ili lozinka.");
        return View(model);
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Verify2Fa(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new Verify2FaViewModel());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verify2Fa(Verify2FaViewModel model, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user is null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _signInManager.TwoFactorSignInAsync(
            TokenOptions.DefaultPhoneProvider,
            model.Code,
            isPersistent: false,
            rememberClient: false);

        if (!result.Succeeded)
        {
            await _audit.LogAsync("LOGIN_OTP_FAILED", "Invalid login 2FA code", user.UserName);
            ModelState.AddModelError(string.Empty, "OTP kod nije validan.");
            return View(model);
        }

        await _audit.LogAsync("LOGIN_OTP_SUCCESS", "Login 2FA success", user.UserName);
        return LocalRedirect(returnUrl ?? Url.Action("Index", "SmsFlow")!);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }
}
