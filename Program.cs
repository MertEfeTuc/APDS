using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---- SERVİSLER (hepsi Build()'den ÖNCE) ----
builder.Services.AddScoped<APDS.Services.AuditLogService>();

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

var app = builder.Build();

// ---- ROL SEED ----
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Akademisyen", "Reviewer", "Admin" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
      // ---- ADMIN KULLANICI SEED ----
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();

    ;
    const string adminEmail = "admin@apds.local";
    const string adminPassword = "Admin123!";

    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new User
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(adminUser, adminPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
if (!dbContext.ActivityTypes.Any())
{
    dbContext.ActivityTypes.AddRange(
        // Yayınlar
        new APDS.Models.ActivityType { Category = "Yayınlar", Name = "SCI Makale", Score = 40 },
        new APDS.Models.ActivityType { Category = "Yayınlar", Name = "SSCI Makale", Score = 40 },
        new APDS.Models.ActivityType { Category = "Yayınlar", Name = "AHCI Makale", Score = 40 },
        new APDS.Models.ActivityType { Category = "Yayınlar", Name = "ESCI Makale", Score = 30 },
        new APDS.Models.ActivityType { Category = "Yayınlar", Name = "Kitap", Score = 30 },
        new APDS.Models.ActivityType { Category = "Yayınlar", Name = "Kitap Bölümü", Score = 15 },
        new APDS.Models.ActivityType { Category = "Yayınlar", Name = "Uluslararası Bildiri", Score = 15 },
        new APDS.Models.ActivityType { Category = "Yayınlar", Name = "Ulusal Bildiri", Score = 8 },

        // Projeler
        new APDS.Models.ActivityType { Category = "Projeler", Name = "TÜBİTAK Yürütücü", Score = 40 },
        new APDS.Models.ActivityType { Category = "Projeler", Name = "TÜBİTAK Araştırmacı", Score = 25 },
        new APDS.Models.ActivityType { Category = "Projeler", Name = "BAP Yürütücü", Score = 20 },
        new APDS.Models.ActivityType { Category = "Projeler", Name = "BAP Araştırmacı", Score = 10 },
        new APDS.Models.ActivityType { Category = "Projeler", Name = "AB Projesi Yürütücü", Score = 50 },

        // Patentler
        new APDS.Models.ActivityType { Category = "Patentler", Name = "Uluslararası Patent", Score = 50 },
        new APDS.Models.ActivityType { Category = "Patentler", Name = "Ulusal Patent", Score = 30 },
        new APDS.Models.ActivityType { Category = "Patentler", Name = "Faydalı Model", Score = 15 },

        // Tezler
        new APDS.Models.ActivityType { Category = "Tezler", Name = "Doktora Tezi", Score = 20 },
        new APDS.Models.ActivityType { Category = "Tezler", Name = "Yüksek Lisans Tezi", Score = 10 }
    );
    await dbContext.SaveChangesAsync();
}
}

// ---- MIDDLEWARE PIPELINE (hepsi Build()'den SONRA) ----

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");
app.Run();