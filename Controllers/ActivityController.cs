using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APDS.Models;

namespace APDS.Controllers
{
    [Authorize(Roles = "Akademisyen")]
    public class ActivityController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly APDS.Services.AuditLogService _auditLog;

       public ActivityController(ApplicationDbContext context, UserManager<User> userManager, APDS.Services.AuditLogService auditLog)
        {
            _context = context;
            _userManager = userManager;
            _auditLog = auditLog;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var model = new ActivityCreateViewModel
            {
                AllActivityTypes = await _context.ActivityTypes.ToListAsync()
            };
            return View(model);
        }

        [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Create(ActivityCreateViewModel model, string submitType)
{
    if (!ModelState.IsValid)
    {
        model.AllActivityTypes = await _context.ActivityTypes.ToListAsync();
        return View(model);
    }

    var user = await _userManager.GetUserAsync(User);

    var activity = new Activity
    {
        Title = model.Title,
        Description = model.Description,
        ActivityDate = model.ActivityDate,
        ActivityTypeId = model.ActivityTypeId,
        AcademicianId = user.Id,
        Status = submitType == "submit" ? ActivityStatus.SUBMITTED : ActivityStatus.DRAFT
    };

    _context.Activities.Add(activity);
    await _context.SaveChangesAsync();
    await _auditLog.LogAsync("Activity", activity.Id.ToString(), "CREATE", user.UserName);
    return RedirectToAction("Index");
}

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            var activities = await _context.Activities
                .Include(a => a.ActivityType)
                .Where(a => a.AcademicianId == user.Id)
                .ToListAsync();

            return View(activities);
        }
        [HttpGet]
public async Task<IActionResult> Details(int id)
{
    var activity = await _context.Activities
        .Include(a => a.ActivityType)
        .Include(a => a.Academician)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (activity == null)
        return NotFound();

    var user = await _userManager.GetUserAsync(User);
    bool isAuthor = user != null && activity.AcademicianId == user.Id;
    bool canEdit = isAuthor &&
        (activity.Status == ActivityStatus.DRAFT || activity.Status == ActivityStatus.REVISION_REQUESTED);

    ViewBag.CanEdit = canEdit;   // view'da butonları göstermek/gizlemek için
    return View(activity);
}

[HttpGet]
[Authorize(Roles = "Akademisyen")]
public async Task<IActionResult> Edit(int id)
{
    var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
    if (activity == null) return NotFound();

    var user = await _userManager.GetUserAsync(User);
    if (activity.AcademicianId != user.Id)
        return Forbid();   // başkasının faaliyetini düzenlemeye çalışıyor

    if (activity.Status != ActivityStatus.DRAFT && activity.Status != ActivityStatus.REVISION_REQUESTED)
        return Forbid();   // durum uygun değil

    var model = new ActivityEditViewModel
    {
        Id = activity.Id,
        Title = activity.Title,
        Description = activity.Description,
        ActivityDate = activity.ActivityDate,
        ActivityTypeId = activity.ActivityTypeId,
        JournalName = activity.JournalName,
        FundingAgency = activity.FundingAgency,
        PatentNumber = activity.PatentNumber,
        ThesisNumber = activity.ThesisNumber,
        AllActivityTypes = await _context.ActivityTypes.ToListAsync()
    };

    return View(model);
}

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Akademisyen")]
public async Task<IActionResult> Edit(ActivityEditViewModel model, string submitType)
{
    if (!ModelState.IsValid)
    {
        model.AllActivityTypes = await _context.ActivityTypes.ToListAsync();
        return View(model);
    }

    var activity = await _context.Activities.FindAsync(model.Id);
    if (activity == null)
        return NotFound();

    var currentUser = await _userManager.GetUserAsync(User);
    if (activity.AcademicianId != currentUser.Id ||
        (activity.Status != ActivityStatus.DRAFT && activity.Status != ActivityStatus.REVISION_REQUESTED))
    {
        return Forbid();
    }

    activity.Title = model.Title;
    activity.Description = model.Description;
    activity.ActivityDate = model.ActivityDate;
    activity.ActivityTypeId = model.ActivityTypeId;
    // ... tür-spesifik alanlar (JournalName vb.) varsa burada güncellenir ...

    if (submitType == "submit")
    {
        // DRAFT'tan geliyorsa SUBMITTED, REVISION_REQUESTED'ten geliyorsa RESUBMITTED
        activity.Status = activity.Status == ActivityStatus.REVISION_REQUESTED
            ? ActivityStatus.RESUBMITTED
            : ActivityStatus.SUBMITTED;
    }
    // submitType == "save" ise Status değişmez, DRAFT/REVISION_REQUESTED olarak kalır

    await _context.SaveChangesAsync();
    await _auditLog.LogAsync("Activity", activity.Id.ToString(), "EDIT", currentUser.UserName);
    return RedirectToAction("Details", new { id = activity.Id });
}

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Akademisyen")]
public async Task<IActionResult> Delete(int id)
{
    var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
    if (activity == null) return NotFound();

    var user = await _userManager.GetUserAsync(User);
    if (activity.AcademicianId != user.Id)
        return Forbid();

    if (activity.Status != ActivityStatus.DRAFT && activity.Status != ActivityStatus.REVISION_REQUESTED)
        return Forbid();

    _context.Activities.Remove(activity);
    await _context.SaveChangesAsync();
    await _auditLog.LogAsync("Activity", id.ToString(), "DELETE", user.UserName);
    return RedirectToAction("Index");
}
    }
}