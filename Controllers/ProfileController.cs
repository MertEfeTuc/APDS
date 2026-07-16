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
        private readonly APDS.Services.IOrcidService _orcidService;
        private readonly APDS.Services.ISemanticScholarService _semanticScholarService;
        private readonly APDS.Services.AuditLogService _auditLog;

        public ProfileController(ApplicationDbContext context, UserManager<User> userManager,
            APDS.Services.IOrcidService orcidService, APDS.Services.ISemanticScholarService semanticScholarService,
            APDS.Services.AuditLogService auditLog)
        {
            _context = context;
            _userManager = userManager;
            _orcidService = orcidService;
            _semanticScholarService = semanticScholarService;
            _auditLog = auditLog;
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
        Bio = user.Bio,
        OrcidId = user.OrcidId
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
    user.OrcidId = model.OrcidId;

    await _userManager.UpdateAsync(user);

    TempData["SuccessMessage"] = "Profil güncellendi.";
    return RedirectToAction("Index");
}
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Akademisyen")]
public async Task<IActionResult> SyncFromOrcid()
{
    var user = await _userManager.GetUserAsync(User);

    if (string.IsNullOrWhiteSpace(user.OrcidId))
    {
        TempData["Error"] = "Senkronizasyon için önce profilinize ORCID ID eklemelisiniz.";
        return RedirectToAction("Edit");
    }
List<APDS.Services.OrcidWorkDto> works;
    try
    {
        works = await _orcidService.GetWorksAsync(user.OrcidId);
    }
    catch (Exception)
    {
        TempData["Error"] = "ORCID senkronizasyonu başarısız oldu.";
        return RedirectToAction("Index");
    }

    var metrics = await _semanticScholarService.GetAuthorMetricsAsync(user.OrcidId);

    var importType = await _context.ActivityTypes.FirstOrDefaultAsync(t => t.Name == "Otomatik İçe Aktarılan Yayın");
    if (importType == null)
    {
        TempData["Error"] = "Sistemde 'Otomatik İçe Aktarılan Yayın' türü tanımlı değil.";
        return RedirectToAction("Index");
    }

    int addedCount = 0;

    foreach (var work in works)
    {
        bool alreadyExists = await _context.Activities.AnyAsync(a =>
            a.AcademicianId == user.Id &&
            ((work.Doi != null && a.Doi == work.Doi) || a.OrcidPutCode == work.PutCode.ToString()));

        if (alreadyExists) continue;

        _context.Activities.Add(new Activity
{
    AcademicianId = user.Id,
    Title = work.Title,
    Description = work.JournalName != null
        ? $"ORCID üzerinden otomatik içe aktarıldı. Dergi: {work.JournalName}"
        : "ORCID üzerinden otomatik içe aktarıldı.",
    JournalName = work.JournalName,
    ActivityDate = work.PublicationDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
    ActivityTypeId = importType.Id,
    Status = ActivityStatus.DRAFT,
    Doi = work.Doi,
    OrcidPutCode = work.PutCode.ToString(),
    CitationCount = metrics?.CitationCount
});

        addedCount++;
    }

    await _context.SaveChangesAsync();
    await _auditLog.LogAsync("Activity", user.Id, "ORCID_SYNC", user.UserName);

    TempData["SuccessMessage"] = $"{addedCount} yeni yayın taslak olarak eklendi.";
    return RedirectToAction("Index");
}
    }
}