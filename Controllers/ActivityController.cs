using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APDS.Models;
using APDS.Services;
using Microsoft.Extensions.Options;
using APDS.Models.Services;
using APDS.Services.PlagiarismCheck;

namespace APDS.Controllers
{
    [Authorize(Roles = "Akademisyen")]
    public class ActivityController : Controller
    {
        private readonly FileStorageSettings _fileStorageSettings;
        private readonly IPdfExtractionService _pdfExtractionService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly APDS.Services.AuditLogService _auditLog;
        private readonly APDS.Services.Notifications.INotificationPublisher _notificationPublisher;
        private readonly APDS.Services.FileStorageSettings _fileStorage;
        private readonly IWebHostEnvironment _env;
        private readonly PlagiarismCheckQueue _plagiarismQueue;

       public ActivityController(ApplicationDbContext context, UserManager<User> userManager,
    APDS.Services.AuditLogService auditLog, APDS.Services.Notifications.INotificationPublisher notificationPublisher,
    Microsoft.Extensions.Options.IOptions<APDS.Services.FileStorageSettings> fileStorageOptions,
    IWebHostEnvironment env, IOptions<FileStorageSettings> fileStorageSettings,   // IOptions<T> pattern kullanılıyorsa
    IPdfExtractionService pdfExtractionService,PlagiarismCheckQueue plagiarismQueue)
{
    _context = context;
    _userManager = userManager;
    _auditLog = auditLog;
    _notificationPublisher = notificationPublisher;
    _fileStorage = fileStorageOptions.Value;
    _env = env;
    _fileStorageSettings = fileStorageSettings.Value;
    _pdfExtractionService = pdfExtractionService;
    _plagiarismQueue = plagiarismQueue;
}

        [HttpGet]
public async Task<IActionResult> Create()
{
    var user = await _userManager.GetUserAsync(User);

    var currentTotal = await _context.Activities
        .Include(a => a.ActivityType)
        .Where(a => a.AcademicianId == user.Id && a.Status == ActivityStatus.APPROVED)
        .SumAsync(a => a.ActivityType.Score);

    ViewBag.CurrentTotal = currentTotal;
    var model = new ActivityCreateViewModel
    {
        AllActivityTypes = await _context.ActivityTypes.ToListAsync()
    };

    if (TempData["ImportedTitle"] != null)
    {
        model.Title = TempData["ImportedTitle"] as string;
        model.JournalName = TempData["ImportedJournal"] as string;
        model.Description = TempData["ImportedDescription"] as string;

        if (TempData["ImportedActivityDate"] is string dateStr && DateOnly.TryParse(dateStr, out var parsedDate))
        {
            model.ActivityDate = parsedDate;
        }
    }

    return View(model);
}
[HttpGet]
public async Task<IActionResult> CopyFromActivity(int id)
{
    var source = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
    if (source == null) return NotFound();

    var user = await _userManager.GetUserAsync(User);
    if (source.AcademicianId != user.Id) return Forbid();

    var model = new ActivityCreateViewModel
    {
        Title = $"{source.Title} (Kopya)",
        Description = source.Description,
        ActivityDate = source.ActivityDate,
        ActivityTypeId = source.ActivityTypeId,
        AllActivityTypes = await _context.ActivityTypes.ToListAsync()
    };

    return View("Create", model);
}
[HttpPost]
[ValidateAntiForgeryToken]
[RequestSizeLimit(50 * 1024 * 1024)] // toplamda makul bir üst sınır, her dosya kendi içinde 10 MB ile sınırlı
public async Task<IActionResult> Create(ActivityCreateViewModel model, string submitType, List<IFormFile> files)
{
    var user = await _userManager.GetUserAsync(User); // ViewBag.CurrentTotal hesaplamak için öne alındı

    if (!ModelState.IsValid)
    {
        model.AllActivityTypes = await _context.ActivityTypes.ToListAsync();
        ViewBag.CurrentTotal = await _context.Activities
            .Include(a => a.ActivityType)
            .Where(a => a.AcademicianId == user.Id && a.Status == ActivityStatus.APPROVED)
            .SumAsync(a => a.ActivityType.Score);
        return View(model);
    }
    var duplicate = await FindDuplicateAsync(model.Title, model.ActivityTypeId, null);
    if (duplicate != null)
    {
        ModelState.AddModelError(string.Empty,
            $"Bu başlık ve türde zaten bir faaliyet kayıtlı (ID: {duplicate.Id}). Mükerrer kayıt oluşturulamaz.");
        model.AllActivityTypes = await _context.ActivityTypes.ToListAsync();
        ViewBag.CurrentTotal = await _context.Activities
            .Include(a => a.ActivityType)
            .Where(a => a.AcademicianId == user.Id && a.Status == ActivityStatus.APPROVED)
            .SumAsync(a => a.ActivityType.Score);
        return View(model);
    }

    Activity activity;
    bool isNewSubmission = submitType == "submit"; // aşağıda plagiarism check tetiklemek için ayrıca lazım
    if (model.Id.HasValue && model.Id.Value > 0)
    {
        activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == model.Id.Value && a.AcademicianId == user.Id);
        if (activity == null) return NotFound();
    }
    else
    {
        activity = new Activity { AcademicianId = user.Id };
        _context.Activities.Add(activity);
    }

    activity.Title = model.Title;
    activity.Description = model.Description;
    activity.ActivityDate = model.ActivityDate;
    activity.ActivityTypeId = model.ActivityTypeId;
    activity.Status = submitType == "submit" ? ActivityStatus.SUBMITTED : ActivityStatus.DRAFT;
    if (activity.Status == ActivityStatus.SUBMITTED)
    {
        activity.LastStatusChangeDate = DateTime.UtcNow;
        activity.OverdueNotificationSent = false;
    }

    if (!ModelState.IsValid)
    {
        model.AllActivityTypes = await _context.ActivityTypes.ToListAsync();
        ViewBag.CurrentTotal = await _context.Activities
            .Include(a => a.ActivityType)
            .Where(a => a.AcademicianId == user.Id && a.Status == ActivityStatus.APPROVED)
            .SumAsync(a => a.ActivityType.Score);
        return View(model);
    }

    // ---- Dosya yükleme (varsa) ----
    if (files != null && files.Any())
    {
        var folder = GetActivityFolder(activity.Id);

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            if (file.Length > _fileStorage.MaxFileSizeBytes)
            {
                TempData["Error"] = $"\"{file.FileName}\" dosyası {_fileStorage.MaxFileSizeBytes / 1024 / 1024} MB sınırını aşıyor, yüklenmedi.";
                continue;
            }

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!_fileStorage.AllowedExtensions.Contains(ext))
            {
                TempData["Error"] = $"\"{file.FileName}\" türüne izin verilmiyor, yüklenmedi.";
                continue;
            }

            var storedFileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(folder, storedFileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _context.ActivityAttachments.Add(new ActivityAttachment
            {
                ActivityId = activity.Id,
                OriginalFileName = file.FileName,
                StoredFileName = storedFileName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                UploadedByUserId = user.Id
            });
        }
    }

    // DÜZELTME: önceden bu satır yalnızca "if (files != null && files.Any())" bloğunun içindeydi,
    // yani dosyasız submit/draft senaryosunda activity hiç DB'ye yazılmıyordu. Artık koşulsuz.
    await _context.SaveChangesAsync();

    // ---- Plagiarism check tetikleme (yalnızca gerçek submit'te, draft'ta değil) ----
    if (isNewSubmission)
    {
        var check = new PlagiarismCheck { ActivityId = activity.Id };
        _context.PlagiarismChecks.Add(check);
        await _context.SaveChangesAsync(); // check.Id burada dolar

        await _plagiarismQueue.EnqueueAsync(new PlagiarismCheckJob
        {
            PlagiarismCheckId = check.Id,
            ActivityId = activity.Id
        });
    }

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
    ViewBag.IsAuthor = isAuthor;   
    ViewBag.Attachments = await _context.ActivityAttachments
    .Where(a => a.ActivityId == id)
    .OrderByDescending(a => a.UploadedDate)
    .ToListAsync();
    return View(activity);
}
[HttpGet]
public async Task<IActionResult> Performance()
{
    var user = await _userManager.GetUserAsync(User);

    var approved = await _context.Activities
        .Include(a => a.ActivityType)
        .Where(a => a.AcademicianId == user.Id && a.Status == ActivityStatus.APPROVED)
        .ToListAsync();

    var years = approved
        .Select(a => a.ActivityDate.Year)
        .Distinct()
        .OrderBy(y => y)
        .ToList();

    var categories = approved
        .Select(a => a.ActivityType.Category)
        .Distinct()
        .OrderBy(c => c)
        .ToList();

    var datasets = categories.Select(cat => new
    {
        label = cat,
        data = years.Select(y => approved
            .Where(a => a.ActivityDate.Year == y && a.ActivityType.Category == cat)
            .Sum(a => a.ActivityType.Score))
            .ToList()
    }).ToList();

    ViewBag.Years = years;
    ViewBag.Datasets = datasets;

    return View();
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
        
var duplicate = await FindDuplicateAsync(model.Title, model.ActivityTypeId, activity.Id);
if (duplicate != null)
{
    ModelState.AddModelError(string.Empty,
        $"Bu başlık ve türde zaten bir faaliyet kayıtlı (ID: {duplicate.Id}). Mükerrer kayıt oluşturulamaz.");
    model.AllActivityTypes = await _context.ActivityTypes.ToListAsync();
    return View(model);
}
    var currentUser = await _userManager.GetUserAsync(User);
    if (activity.AcademicianId != currentUser.Id ||
        (activity.Status != ActivityStatus.DRAFT && activity.Status != ActivityStatus.REVISION_REQUESTED))
    {
        return Forbid();
    }
var lastVersionNumber = await _context.ActivityVersions
    .Where(v => v.ActivityId == activity.Id)
    .Select(v => (int?)v.VersionNumber)
    .MaxAsync() ?? 0;

    _context.ActivityVersions.Add(new ActivityVersion
    {
        ActivityId = activity.Id,
        VersionNumber = lastVersionNumber + 1,
        Title = activity.Title,
        Description = activity.Description,
        ActivityDate = activity.ActivityDate,
        Status = activity.Status,
        ActivityTypeId = activity.ActivityTypeId,
        JournalName = activity.JournalName,
        FundingAgency = activity.FundingAgency,
        PatentNumber = activity.PatentNumber,
        ThesisNumber = activity.ThesisNumber,
        SnapshotReason = activity.Status == ActivityStatus.REVISION_REQUESTED ? "RESUBMIT" : "EDIT"
    });

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
        activity.LastStatusChangeDate = DateTime.UtcNow;
        activity.OverdueNotificationSent = false;   
        
    }
    // submitType == "save" ise Status değişmez, DRAFT/REVISION_REQUESTED olarak kalır

    await _context.SaveChangesAsync();
    await _auditLog.LogAsync("Activity", activity.Id.ToString(), "EDIT", currentUser.UserName);
 

// ---- Bildirim: yeniden gönderildiyse reviewer'a haber ver ----
if (submitType == "submit")
{
    var assignment = await _context.ReviewerAssignments
        .FirstOrDefaultAsync(a => a.AcademicianId == activity.AcademicianId);

    if (assignment != null)
    {
        await _notificationPublisher.PublishAsync(new APDS.Events.ActivityStatusChangedEvent(
            RecipientUserId: assignment.ReviewerId,
            Type: NotificationType.ASSIGNMENT,
            Message: $"{currentUser.UserName} bir faaliyeti gönderdi: \"{activity.Title}\"",
            RelatedEntityId: activity.Id
        ));
    }
}

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

[HttpGet]
[Authorize(Roles = "Akademisyen,Reviewer")]
public async Task<IActionResult> VersionHistory(int id)
{
    var activity = await _context.Activities
        .Include(a => a.ActivityType)
        .Include(a => a.Academician)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (activity == null) return NotFound();
    if (!await CanAccessActivity(activity)) return Forbid();

    var versions = await _context.ActivityVersions
        .Where(v => v.ActivityId == id)
        .OrderByDescending(v => v.VersionNumber)
        .ToListAsync();

    ViewBag.Activity = activity;
    return View(versions);
}

[HttpGet]
[Authorize(Roles = "Akademisyen,Reviewer")]
public async Task<IActionResult> CompareVersions(int activityId, int oldVersionId, int? newVersionId)
{
    var activity = await _context.Activities
        .Include(a => a.ActivityType)
        .FirstOrDefaultAsync(a => a.Id == activityId);

    if (activity == null) return NotFound();
    if (!await CanAccessActivity(activity)) return Forbid();

    var oldVersion = await _context.ActivityVersions.FirstOrDefaultAsync(v => v.Id == oldVersionId);
    if (oldVersion == null) return NotFound();

    ActivityVersion newVersion = newVersionId.HasValue
        ? await _context.ActivityVersions.FirstOrDefaultAsync(v => v.Id == newVersionId)
        : null;

    var allTypes = await _context.ActivityTypes.ToDictionaryAsync(t => t.Id, t => t.Name);

    var rows = new List<VersionCompareRow>
    {
        new("Başlık", oldVersion.Title, newVersion?.Title ?? activity.Title),
        new("Açıklama", oldVersion.Description, newVersion?.Description ?? activity.Description),
        new("Tarih", oldVersion.ActivityDate.ToString("dd.MM.yyyy"), (newVersion?.ActivityDate ?? activity.ActivityDate).ToString("dd.MM.yyyy")),
        new("Faaliyet Türü", allTypes.GetValueOrDefault(oldVersion.ActivityTypeId, "-"), allTypes.GetValueOrDefault(newVersion?.ActivityTypeId ?? activity.ActivityTypeId, "-")),
        new("Durum", oldVersion.Status.ToString(), (newVersion?.Status ?? activity.Status).ToString()),
        new("Dergi Adı", oldVersion.JournalName, newVersion?.JournalName ?? activity.JournalName),
        new("Fon Kurumu", oldVersion.FundingAgency, newVersion?.FundingAgency ?? activity.FundingAgency),
        new("Patent No", oldVersion.PatentNumber, newVersion?.PatentNumber ?? activity.PatentNumber),
        new("Tez No", oldVersion.ThesisNumber, newVersion?.ThesisNumber ?? activity.ThesisNumber),
    };

    ViewBag.Activity = activity;
    ViewBag.OldLabel = $"Versiyon {oldVersion.VersionNumber} ({oldVersion.SnapshotDate:dd.MM.yyyy HH:mm})";
    ViewBag.NewLabel = newVersion != null
        ? $"Versiyon {newVersion.VersionNumber} ({newVersion.SnapshotDate:dd.MM.yyyy HH:mm})"
        : "Güncel Hal";

    return View(rows);
}

private async Task<bool> CanAccessActivity(Activity activity)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return false;

    if (activity.AcademicianId == user.Id) return true;

    return await _context.ReviewerAssignments
        .AnyAsync(a => a.ReviewerId == user.Id && a.AcademicianId == activity.AcademicianId);
}
private string GetActivityFolder(int activityId)
{
    var root = Path.Combine(_env.ContentRootPath, _fileStorage.RootPath, activityId.ToString());
    Directory.CreateDirectory(root);
    return root;
}

[HttpPost]
[Authorize(Roles = "Akademisyen")]
[ValidateAntiForgeryToken]
[RequestSizeLimit(10 * 1024 * 1024)]
public async Task<IActionResult> UploadAttachment(int activityId, IFormFile file)
{
    var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == activityId);
    if (activity == null) return NotFound();

    var currentUser = await _userManager.GetUserAsync(User);
    if (activity.AcademicianId != currentUser.Id) return Forbid();

    if (activity.Status != ActivityStatus.DRAFT && activity.Status != ActivityStatus.REVISION_REQUESTED)
    {
        TempData["Error"] = "Dosya sadece taslak veya revizyon istenen faaliyetlere yüklenebilir.";
        return RedirectToAction("Details", new { id = activityId });
    }

    if (file == null || file.Length == 0)
    {
        TempData["Error"] = "Dosya seçilmedi.";
        return RedirectToAction("Details", new { id = activityId });
    }

    if (file.Length > _fileStorage.MaxFileSizeBytes)
    {
        TempData["Error"] = $"Dosya boyutu {_fileStorage.MaxFileSizeBytes / 1024 / 1024} MB sınırını aşıyor.";
        return RedirectToAction("Details", new { id = activityId });
    }

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!_fileStorage.AllowedExtensions.Contains(ext))
    {
        TempData["Error"] = "Bu dosya türüne izin verilmiyor.";
        return RedirectToAction("Details", new { id = activityId });
    }

    var storedFileName = $"{Guid.NewGuid()}{ext}";
    var folder = GetActivityFolder(activityId);
    var fullPath = Path.Combine(folder, storedFileName);

    using (var stream = new FileStream(fullPath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }

    _context.ActivityAttachments.Add(new ActivityAttachment
    {
        ActivityId = activityId,
        OriginalFileName = file.FileName,
        StoredFileName = storedFileName,
        ContentType = file.ContentType,
        FileSizeBytes = file.Length,
        UploadedByUserId = currentUser.Id
    });

    await _context.SaveChangesAsync();
    await _auditLog.LogAsync("ActivityAttachment", activityId.ToString(), "UPLOAD", currentUser.UserName);

    return RedirectToAction("Details", new { id = activityId });
}
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ExtractFromPdf(IFormFile file)
{
    // FileStorageSettings'teki aynı kısıtlar: 10MB, sadece .pdf
    if (file.Length > _fileStorageSettings.MaxFileSizeBytes)
        return BadRequest("Dosya boyutu sınırı aşıldı");

    using var ms = new MemoryStream();
    await file.CopyToAsync(ms);
    if (file == null || file.Length == 0)
    return BadRequest("Dosya bulunamadı");

    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (ext != ".pdf")
        return BadRequest("Sadece PDF dosyaları kabul edilir");
    var result = await _pdfExtractionService.ExtractFromPdfAsync(ms.ToArray(), file.FileName);

    return Json(result); // camelCase serializer ayarına dikkat
}

[HttpGet]
[Authorize(Roles = "Akademisyen,Reviewer")]
public async Task<IActionResult> DownloadAttachment(int id)
{
    var attachment = await _context.ActivityAttachments
        .Include(a => a.Activity)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (attachment == null) return NotFound();
    if (!await CanAccessActivity(attachment.Activity)) return Forbid();

    var fullPath = Path.Combine(GetActivityFolder(attachment.ActivityId), attachment.StoredFileName);
    if (!System.IO.File.Exists(fullPath)) return NotFound();

    var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
    return File(bytes, attachment.ContentType, attachment.OriginalFileName);
}
[HttpGet]
public IActionResult ImportFromPdf()
{
    return View();
}

[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult ConfirmImport(string? title, string? journal, string? doi, string? authors, string? publicationDate)
{
    TempData["ImportedTitle"] = title;
    TempData["ImportedJournal"] = journal;
    TempData["ImportedActivityDate"] = publicationDate;

    var descParts = new List<string>();
    if (!string.IsNullOrWhiteSpace(authors)) descParts.Add($"Yazarlar: {authors}");
    if (!string.IsNullOrWhiteSpace(doi)) descParts.Add($"DOI: {doi}");
    TempData["ImportedDescription"] = string.Join("\n", descParts);

    return RedirectToAction("Create");
}
[HttpPost]
[Authorize(Roles = "Akademisyen")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteAttachment(int id)
{
    var attachment = await _context.ActivityAttachments
        .Include(a => a.Activity)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (attachment == null) return NotFound();

    var currentUser = await _userManager.GetUserAsync(User);
    if (attachment.Activity.AcademicianId != currentUser.Id) return Forbid();

    if (attachment.Activity.Status != ActivityStatus.DRAFT && attachment.Activity.Status != ActivityStatus.REVISION_REQUESTED)
    {
        TempData["Error"] = "Sadece taslak veya revizyon istenen faaliyetlerden dosya silinebilir.";
        return RedirectToAction("Details", new { id = attachment.ActivityId });
    }

    var fullPath = Path.Combine(GetActivityFolder(attachment.ActivityId), attachment.StoredFileName);
    if (System.IO.File.Exists(fullPath)) System.IO.File.Delete(fullPath);

    var activityId = attachment.ActivityId;
    _context.ActivityAttachments.Remove(attachment);
    await _context.SaveChangesAsync();
    await _auditLog.LogAsync("ActivityAttachment", activityId.ToString(), "DELETE", currentUser.UserName);

    return RedirectToAction("Details", new { id = activityId });
}
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> AutoSave(int? id, string title, string description,
    DateOnly? activityDate, int? activityTypeId,
    string? journalName, string? fundingAgency, string? patentNumber, string? thesisNumber)
{
    var user = await _userManager.GetUserAsync(User);
    if (user == null) return Unauthorized();

    Activity activity;

    if (id == null || id == 0)
    {
        // Başlık ya da tür yoksa boş taslak oluşturma
        if (string.IsNullOrWhiteSpace(title) && activityTypeId == null)
            return Json(new { id = (int?)null });

        activity = new Activity
        {
            AcademicianId = user.Id,
            Status = ActivityStatus.DRAFT,
            ActivityTypeId = activityTypeId ?? 0,
            ActivityDate = activityDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
        };
        _context.Activities.Add(activity);
    }
    else
    {
        activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null) return NotFound();
        if (activity.AcademicianId != user.Id) return Forbid();
        if (activity.Status != ActivityStatus.DRAFT && activity.Status != ActivityStatus.REVISION_REQUESTED)
            return Json(new { id = activity.Id, skipped = true }); // submit edilmiş, otomatik kaydetme

        if (activityTypeId.HasValue) activity.ActivityTypeId = activityTypeId.Value;
        if (activityDate.HasValue) activity.ActivityDate = activityDate.Value;
    }

    activity.Title = title ?? string.Empty;
    activity.Description = description;
    activity.JournalName = journalName;
    activity.FundingAgency = fundingAgency;
    activity.PatentNumber = patentNumber;
    activity.ThesisNumber = thesisNumber;

    await _context.SaveChangesAsync();

    return Json(new { id = activity.Id, savedAt = DateTime.Now.ToString("HH:mm:ss") });
}
private async Task<Activity> FindDuplicateAsync(string title, int activityTypeId, int? excludeActivityId)
{
    var normalizedTitle = title?.Trim().ToLower() ?? string.Empty;

    var query = _context.Activities
        .Where(a => a.ActivityTypeId == activityTypeId)
        .Where(a => a.Status != ActivityStatus.DRAFT);

    if (excludeActivityId.HasValue)
        query = query.Where(a => a.Id != excludeActivityId.Value);

    var candidates = await query.Select(a => new { a.Id, a.Title }).ToListAsync();

    return candidates
        .Where(a => a.Title != null && a.Title.Trim().ToLower() == normalizedTitle)
        .Select(a => new Activity { Id = a.Id, Title = a.Title })
        .FirstOrDefault();
}
    }
}