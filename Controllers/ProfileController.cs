using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APDS.Models;

namespace APDS.Controllers
{
    [Authorize(Roles = "Akademisyen,Reviewer")]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public ProfileController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var targetId = id ?? currentUser.Id;

            var profileUser = await _context.Users
                .Include(u => u.Department)
                    .ThenInclude(d => d.Faculty)
                .FirstOrDefaultAsync(u => u.Id == targetId);

            if (profileUser == null) return NotFound();

            var isAcademician = await _userManager.IsInRoleAsync(profileUser, "Akademisyen");
            if (!isAcademician) return NotFound(); // sadece akademisyen profilleri var

            var approvedActivities = await _context.Activities
                .Include(a => a.ActivityType)
                .Where(a => a.AcademicianId == targetId && a.Status == ActivityStatus.APPROVED)
                .OrderByDescending(a => a.ActivityDate)
                .ToListAsync();

            var years = approvedActivities.Select(a => a.ActivityDate.Year).Distinct().OrderBy(y => y).ToList();
            var categories = approvedActivities.Select(a => a.ActivityType.Category).Distinct().OrderBy(c => c).ToList();

            var datasets = categories.Select(cat => new
            {
                label = cat,
                data = years.Select(y => approvedActivities
                    .Where(a => a.ActivityDate.Year == y && a.ActivityType.Category == cat)
                    .Sum(a => a.ActivityType.Score))
                    .ToList()
            }).ToList();

            ViewBag.ProfileUser = profileUser;
            ViewBag.TotalScore = approvedActivities.Sum(a => a.ActivityType.Score);
            ViewBag.TotalApprovedCount = approvedActivities.Count;
            ViewBag.Years = years;
            ViewBag.Datasets = datasets;
            ViewBag.IsOwnProfile = targetId == currentUser.Id;

            return View(approvedActivities);
        }
        [HttpGet]
[Authorize(Roles = "Akademisyen")]
public async Task<IActionResult> Edit()
{
    var user = await _userManager.GetUserAsync(User);

    var model = new ProfileEditViewModel
    {
        Title = user.Title,
        Bio = user.Bio
    };

    return View(model);
}

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Akademisyen")]
public async Task<IActionResult> Edit(ProfileEditViewModel model)
{
    if (!ModelState.IsValid)
        return View(model);

    var user = await _userManager.GetUserAsync(User);
    user.Title = model.Title;
    user.Bio = model.Bio;

    await _userManager.UpdateAsync(user);

    TempData["SuccessMessage"] = "Profil güncellendi.";
    return RedirectToAction("Index");
}
    }
}