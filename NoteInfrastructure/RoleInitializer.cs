using Microsoft.AspNetCore.Identity;
using NoteInfrastructure.Models;

namespace NoteInfrastructure;

public static class RoleInitializer
{
    public static async Task InitializeAsync(
        UserManager<AppUser>      userManager,
        RoleManager<IdentityRole> roleManager)
    {

        foreach (var roleName in new[] { "admin", "user" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        const string adminEmail    = "admin@codenote.ua";
        const string adminPassword = "Admin_1234";

        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new AppUser { Email = adminEmail, UserName = adminEmail };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (result.Succeeded)
                await userManager.AddToRoleAsync(admin, "admin");
        }
    }
}
