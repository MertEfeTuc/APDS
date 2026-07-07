using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using APDS.Services.Notifications;
using APDS.Services;

var builder = WebApplication.CreateBuilder(args);

// ---- SERVİSLER (hepsi Build()'den ÖNCE) ----
builder.Services.AddScoped<APDS.Services.AuditLogService>();

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHttpClient<APDS.Services.IOrcidService, APDS.Services.OrcidService>();
builder.Services.AddHttpClient<APDS.Services.ISemanticScholarService, APDS.Services.SemanticScholarService>();
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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});
builder.Services.Configure<APDS.Services.FileStorageSettings>(builder.Configuration.GetSection("FileStorage"));
builder.Services.Configure<SmtpSettings>(builder.Configuration.GetSection("Smtp"));
builder.Services.AddSingleton<NotificationQueue>();
builder.Services.AddScoped<INotificationPublisher, NotificationPublisher>();
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
builder.Services.AddHostedService<DailyDigestService>();
builder.Services.AddHostedService<OverdueCheckService>();
builder.Services.AddHostedService<NotificationProcessor>();


var app = builder.Build();

new APDS.Models.ActivityType { Category = "Yayınlar", Name = "Otomatik İçe Aktarılan Yayın", Score = 10 };
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
var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
var adminEmail = config["AdminSeed:Email"]!;
var adminPassword = config["AdminSeed:Password"]!;
    ;
   

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
if (!dbContext.ActivityTypes.Any(t => t.Name == "Otomatik İçe Aktarılan Yayın"))
{
    dbContext.ActivityTypes.Add(
        new APDS.Models.ActivityType { Category = "Yayınlar", Name = "Otomatik İçe Aktarılan Yayın", Score = 10 }
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