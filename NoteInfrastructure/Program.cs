using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NoteInfrastructure;
using NoteInfrastructure.Models;

var builder = WebApplication.CreateBuilder(args);

// ── MVC ───────────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// ── Основна БД (NotedbContext — PostgreSQL) ───────────────────────────────
builder.Services.AddDbContext<NotedbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Identity БД (IdentityContext — PostgreSQL) ────────────────────────────
builder.Services.AddDbContext<IdentityContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("IdentityConnection")));

// ── ASP.NET Core Identity ─────────────────────────────────────────────────
builder.Services
    .AddIdentity<AppUser, IdentityRole>(options =>
    {
        // Вимоги до пароля
        options.Password.RequireDigit           = true;
        options.Password.RequiredLength         = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase       = false;

        // Блокування після невдалих спроб
        options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers      = true;
    })
    .AddEntityFrameworkStores<IdentityContext>()
    .AddDefaultTokenProviders();

// ── Перенаправлення на сторінку входу за замовчуванням ────────────────────
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath  = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
});

var app = builder.Build();

// ── Ініціалізація ролей і адміна ──────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var userManager  = services.GetRequiredService<UserManager<AppUser>>();
        var roleManager  = services.GetRequiredService<RoleManager<IdentityRole>>();
        await RoleInitializer.InitializeAsync(userManager, roleManager);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Помилка під час ініціалізації бази даних: {Time}", DateTime.Now);
    }
}

// ── HTTP Pipeline ─────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();          // ← має бути ПЕРЕД UseAuthentication та UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllers();

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Folders}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
