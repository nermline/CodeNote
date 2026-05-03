using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NoteInfrastructure.Models;
using NoteInfrastructure.ViewModels;

namespace NoteInfrastructure.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<AppUser>   _userManager;
    private readonly SignInManager<AppUser> _signInManager;

    public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
    {
        _userManager   = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public IActionResult Register() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = new AppUser
        {
            Email    = model.Email,
            UserName = model.Email,
            Year     = model.Year
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "user");
            await _signInManager.SignInAsync(user, isPersistent: false);
            return RedirectToAction("Index", "Folders");
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return View(model);
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
        => View(new LoginViewModel { ReturnUrl = returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _signInManager.PasswordSignInAsync(
            model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Index", "Folders");
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty,
                "Акаунт тимчасово заблоковано через надто багато невдалих спроб. " +
                "Спробуйте через 15 хвилин.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "Неправильний логін чи пароль");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Folders");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var model = new EditProfileViewModel
        {
            Email = user.Email ?? string.Empty,
            Year  = user.Year
        };

        ViewData["UserRoles"] = await _userManager.GetRolesAsync(user);
        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var cu = await _userManager.GetUserAsync(User);
            if (cu is not null) ViewData["UserRoles"] = await _userManager.GetRolesAsync(cu);
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (!string.Equals(user.Email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            var existing = await _userManager.FindByEmailAsync(model.Email);
            if (existing is not null && existing.Id != user.Id)
            {
                ModelState.AddModelError("Email", "Цей email вже використовується.");
                ViewData["UserRoles"] = await _userManager.GetRolesAsync(user);
                return View(model);
            }

            user.Email    = model.Email;
            user.UserName = model.Email;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var e in updateResult.Errors)
                    ModelState.AddModelError(string.Empty, e.Description);
                ViewData["UserRoles"] = await _userManager.GetRolesAsync(user);
                return View(model);
            }
            await _signInManager.RefreshSignInAsync(user);
        }

        user.Year = model.Year;
        await _userManager.UpdateAsync(user);

        TempData["SuccessMessage"] = "Профіль успішно оновлено.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View();

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(
            user, model.CurrentPassword, model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["SuccessMessage"] = "Пароль успішно змінено.";
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet]
    public IActionResult DeleteAccount() => View();

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccountConfirmed(string confirmation)
    {
        if (!string.Equals(confirmation, "ВИДАЛИТИ", StringComparison.Ordinal))
        {
            ModelState.AddModelError(string.Empty,
                "Для підтвердження введіть слово ВИДАЛИТИ великими літерами.");
            return View("DeleteAccount");
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return NotFound();

        if (await _userManager.IsInRoleAsync(user, "admin"))
        {
            ModelState.AddModelError(string.Empty,
                "Адміністратор не може видалити власний акаунт.");
            return View("DeleteAccount");
        }

        await _signInManager.SignOutAsync();
        await _userManager.DeleteAsync(user);
        return RedirectToAction("Index", "Folders");
    }
}
