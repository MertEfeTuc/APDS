using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using APDS.Models;

namespace APDS.Controllers
{
    [Authorize]
    public class SecurityController : Controller
    {
        private readonly UserManager<User> _userManager;

        public SecurityController(UserManager<User> userManager)
        {
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var model = new SecurityIndexViewModel
            {
                TwoFactorEnabled = user.TwoFactorEnabled,
                RecoveryCodesLeft = user.TwoFactorEnabled
                    ? await _userManager.CountRecoveryCodesAsync(user)
                    : 0
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> EnableAuthenticator()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            // Onaylanana kadar her ziyarette yeni anahtar üretmek güvenlik açısından sorun değil,
            // zaten TwoFactorEnabled=false olduğu sürece kullanılamıyor.
            await _userManager.ResetAuthenticatorKeyAsync(user);
            var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);

            return View(BuildAuthenticatorModel(user, unformattedKey!));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EnableAuthenticator(EnableAuthenticatorViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            var key = await _userManager.GetAuthenticatorKeyAsync(user);

            if (!ModelState.IsValid)
            {
                return View(RebuildWithError(user, key!, model.Code));
            }

            var verificationCode = (model.Code ?? string.Empty)
                .Replace(" ", string.Empty)
                .Replace("-", string.Empty);

            var isValid = await _userManager.VerifyTwoFactorTokenAsync(
                user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

            if (!isValid)
            {
                ModelState.AddModelError(nameof(model.Code), "Doğrulama kodu geçersiz.");
                return View(RebuildWithError(user, key!, model.Code));
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);
            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);

            TempData["RecoveryCodes"] = string.Join(",", recoveryCodes!);
            return RedirectToAction(nameof(ShowRecoveryCodes));
        }

        [HttpGet]
        public IActionResult ShowRecoveryCodes()
        {
            if (TempData.Peek("RecoveryCodes") is not string codesJoined)
                return RedirectToAction(nameof(Index));

            return View(codesJoined.Split(','));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable2FA()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            await _userManager.SetTwoFactorEnabledAsync(user, false);
            await _userManager.ResetAuthenticatorKeyAsync(user);

            TempData["Message"] = "İki faktörlü kimlik doğrulama kapatıldı.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegenerateRecoveryCodes()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();
            if (!user.TwoFactorEnabled) return RedirectToAction(nameof(Index));

            var recoveryCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            TempData["RecoveryCodes"] = string.Join(",", recoveryCodes!);
            return RedirectToAction(nameof(ShowRecoveryCodes));
        }

        private EnableAuthenticatorViewModel RebuildWithError(User user, string unformattedKey, string? enteredCode)
        {
            var model = BuildAuthenticatorModel(user, unformattedKey);
            model.Code = enteredCode;
            return model;
        }

        private EnableAuthenticatorViewModel BuildAuthenticatorModel(User user, string unformattedKey)
        {
            const string issuer = "APDS";
            var label = user.Email ?? user.UserName ?? "kullanici";

            var authenticatorUri = string.Format(
                "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
                Uri.EscapeDataString(issuer),
                Uri.EscapeDataString(label),
                unformattedKey);

            return new EnableAuthenticatorViewModel
            {
                SharedKey = FormatKey(unformattedKey),
                AuthenticatorUri = authenticatorUri
            };
        }

        private static string FormatKey(string unformattedKey)
        {
            var result = new StringBuilder();
            var pos = 0;
            while (pos + 4 < unformattedKey.Length)
            {
                result.Append(unformattedKey.AsSpan(pos, 4)).Append(' ');
                pos += 4;
            }
            if (pos < unformattedKey.Length)
                result.Append(unformattedKey.AsSpan(pos));

            return result.ToString().ToLowerInvariant();
        }
    }
}