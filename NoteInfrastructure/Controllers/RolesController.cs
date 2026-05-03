using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using NoteInfrastructure.Models;
using NoteInfrastructure.ViewModels;

namespace NoteInfrastructure.Controllers;

[Authorize(Roles = "admin")]
public class RolesController : Controller
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<AppUser>      _userManager;

    public RolesController(RoleManager<IdentityRole> roleManager, UserManager<AppUser> userManager)
    {
        _roleManager = roleManager;
        _userManager = userManager;
    }

    public IActionResult Index() => View(_roleManager.Roles.ToList());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string roleName)
    {
        if (!string.IsNullOrWhiteSpace(roleName))
        {
            var trimmed = roleName.Trim();
            if (!await _roleManager.RoleExistsAsync(trimmed))
                await _roleManager.CreateAsync(new IdentityRole(trimmed));
            else
                TempData["ErrorMessage"] = $"Роль «{trimmed}» вже існує.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is not null)
        {

            if (role.Name is "admin" or "user")
            {
                TempData["ErrorMessage"] = "Системні ролі «admin» та «user» видаляти не можна.";
                return RedirectToAction(nameof(Index));
            }
            await _roleManager.DeleteAsync(role);
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> UserList(string? search)
    {
        ViewData["Search"] = search;

        var users = string.IsNullOrWhiteSpace(search)
            ? _userManager.Users.ToList()
            : _userManager.Users
                .Where(u => u.Email != null && u.Email.Contains(search))
                .ToList();

        var viewModels = new List<AdminUserViewModel>();
        var allAdmins  = await _userManager.GetUsersInRoleAsync("admin");

        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            viewModels.Add(new AdminUserViewModel
            {
                Id          = u.Id,
                Email       = u.Email ?? "—",
                Year        = u.Year,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                IsLastAdmin = roles.Contains("admin") && allAdmins.Count == 1,
                Roles       = roles
            });
        }

        return View(viewModels);
    }

    public async Task<IActionResult> Edit(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var model = new ChangeRoleViewModel
        {
            UserId    = user.Id,
            UserEmail = user.Email ?? string.Empty,
            UserRoles = await _userManager.GetRolesAsync(user),
            AllRoles  = _roleManager.Roles.ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string userId, List<string> roles)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (!roles.Contains("admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("admin");
            if (admins.Count == 1 && admins[0].Id == userId)
            {
                TempData["ErrorMessage"] = "Неможливо прибрати роль «admin» у єдиного адміністратора.";
                return RedirectToAction(nameof(UserList));
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.AddToRolesAsync(user, roles.Except(currentRoles));
        await _userManager.RemoveFromRolesAsync(user, currentRoles.Except(roles));

        TempData["SuccessMessage"] = $"Ролі користувача {user.Email} оновлено.";
        return RedirectToAction(nameof(UserList));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        return View(new AdminResetPasswordViewModel
        {
            UserId    = user.Id,
            UserEmail = user.Email ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(AdminResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByIdAsync(model.UserId);
        if (user is null) return NotFound();

        var token  = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        TempData["SuccessMessage"] = $"Пароль користувача {user.Email} успішно скинуто.";
        return RedirectToAction(nameof(UserList));
    }

    [HttpGet]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (await _userManager.IsInRoleAsync(user, "admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("admin");
            if (admins.Count <= 1)
            {
                TempData["ErrorMessage"] = "Неможливо видалити єдиного адміністратора.";
                return RedirectToAction(nameof(UserList));
            }
        }

        return View(new AdminUserViewModel
        {
            Id    = user.Id,
            Email = user.Email ?? "—",
            Year  = user.Year,
            Roles = await _userManager.GetRolesAsync(user)
        });
    }

    [HttpPost, ActionName("DeleteUser")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUserConfirmed(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (await _userManager.IsInRoleAsync(user, "admin"))
        {
            var admins = await _userManager.GetUsersInRoleAsync("admin");
            if (admins.Count <= 1)
            {
                TempData["ErrorMessage"] = "Неможливо видалити єдиного адміністратора.";
                return RedirectToAction(nameof(UserList));
            }
        }

        await _userManager.DeleteAsync(user);
        TempData["SuccessMessage"] = $"Користувача {user.Email} видалено.";
        return RedirectToAction(nameof(UserList));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        if (await _userManager.IsInRoleAsync(user, "admin"))
        {
            TempData["ErrorMessage"] = "Неможливо заблокувати адміністратора.";
            return RedirectToAction(nameof(UserList));
        }

        bool isLocked = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
        if (isLocked)
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
            TempData["SuccessMessage"] = $"Користувача {user.Email} розблоковано.";
        }
        else
        {
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            TempData["SuccessMessage"] = $"Користувача {user.Email} заблоковано.";
        }

        return RedirectToAction(nameof(UserList));
    }
}
