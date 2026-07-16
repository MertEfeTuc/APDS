using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using APDS.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace APDS.Controllers
{
    public class AccountController : Controller
    {
        private readonly SignInManager<User> _signInManager;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context; 
        public AccountController(SignInManager<User> signInManager, UserManager<User> userManager, ApplicationDbContext context)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context; 
        }
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Register()
    {
        var model = new RegisterViewModel
        {
            AllDepartments = await _context.Departments.Include(d => d.Faculty).OrderBy(d => d.Name).ToListAsync()
        };
        return View(model);
    }

   [HttpPost]
[AllowAnonymous]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Register(RegisterViewModel model)
{
    if (model.SelectedRole != "Akademisyen" && model.SelectedRole != "Reviewer")
    {
        ModelState.AddModelError(nameof(model.SelectedRole), "Geçersiz hesap türü.");
    }

    if (model.SelectedRole == "Akademisyen" && model.DepartmentId == null)
    {
        ModelState.AddModelError(nameof(model.DepartmentId), "Akademisyenler için bölüm seçimi zorunludur.");
    }

    if (!ModelState.IsValid)
    {
        model.AllDepartments = await _context.Departments.Include(d => d.Faculty).OrderBy(d => d.Name).ToListAsync();
        return View(model);
    }

    var user = new User
    {
        UserName = model.Username,
        Email = model.Email,
        DepartmentId = model.SelectedRole == "Akademisyen" ? model.DepartmentId : null
    };

    var result = await _userManager.CreateAsync(user, model.Password);

    if (result.Succeeded)
    {
        await _userManager.AddToRoleAsync(user, model.SelectedRole);
        await _signInManager.SignInAsync(user, isPersistent: false);
        return await RedirectByRole(user);
    }

    foreach (var error in result.Errors)
        ModelState.AddModelError(string.Empty, error.Description);

    model.AllDepartments = await _context.Departments.Include(d => d.Faculty).OrderBy(d => d.Name).ToListAsync();
    return View(model);
}
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login()
        {
            return View();
        }

        [AllowAnonymous]
        public IActionResult AccessDenied()
        {
            return View();
        }

        [HttpPost]
[AllowAnonymous]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginViewModel model)
{
    if (!ModelState.IsValid)
        return View(model);

    var result = await _signInManager.PasswordSignInAsync(
        model.Username, model.Password, model.RememberMe, lockoutOnFailure: false);

    if (result.Succeeded)
    {
        var user = await _userManager.FindByNameAsync(model.Username);
        return await RedirectByRole(user);
    }

    if (result.RequiresTwoFactor)
    {
        return RedirectToAction(nameof(TwoFactorLogin), new { rememberMe = model.RememberMe });
    }

    ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre yanlış.");
    return View(model);
}

[HttpGet]
[AllowAnonymous]
public async Task<IActionResult> TwoFactorLogin(bool rememberMe)
{
    var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
    if (user == null)
        return RedirectToAction(nameof(Login));

    return View(new TwoFactorLoginViewModel { RememberMe = rememberMe });
}

[HttpPost]
[AllowAnonymous]
[ValidateAntiForgeryToken]
public async Task<IActionResult> TwoFactorLogin(TwoFactorLoginViewModel model)
{
    if (!ModelState.IsValid)
        return View(model);

    var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
    if (user == null)
        return RedirectToAction(nameof(Login));

    var code = model.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

    var result = await _signInManager.TwoFactorAuthenticatorSignInAsync(
        code, model.RememberMe, model.RememberMachine);

    if (result.Succeeded)
        return await RedirectByRole(user);

    if (result.IsLockedOut)
    {
        ModelState.AddModelError(string.Empty, "Hesap kilitlendi, lütfen daha sonra tekrar deneyin.");
        return View(model);
    }

    ModelState.AddModelError(string.Empty, "Doğrulama kodu geçersiz.");
    return View(model);
}

[HttpGet]
[AllowAnonymous]
public async Task<IActionResult> RecoveryCodeLogin()
{
    var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
    if (user == null)
        return RedirectToAction(nameof(Login));

    return View(new RecoveryCodeLoginViewModel());
}

[HttpPost]
[AllowAnonymous]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RecoveryCodeLogin(RecoveryCodeLoginViewModel model)
{
    if (!ModelState.IsValid)
        return View(model);

    var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
    if (user == null)
        return RedirectToAction(nameof(Login));

    var code = model.RecoveryCode.Replace(" ", string.Empty);
    var result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(code);

    if (result.Succeeded)
        return await RedirectByRole(user);

    if (result.IsLockedOut)
    {
        ModelState.AddModelError(string.Empty, "Hesap kilitlendi, lütfen daha sonra tekrar deneyin.");
        return View(model);
    }

    ModelState.AddModelError(string.Empty, "Kurtarma kodu geçersiz veya daha önce kullanılmış.");
    return View(model);
}

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login", "Account");
        }
        private async Task<IActionResult> RedirectByRole(User user)
        {
            if (await _userManager.IsInRoleAsync(user, "Akademisyen"))
                return RedirectToAction("AkademisyenPaneli", "Home");

            if (await _userManager.IsInRoleAsync(user, "Reviewer"))
                return RedirectToAction("ReviewerPaneli", "Home");

            if (await _userManager.IsInRoleAsync(user, "Admin"))
                return RedirectToAction("Index", "Home"); // Admin paneli ileride eklenecek

            return RedirectToAction("Index", "Home"); // fallback
        }   
    }
    
}