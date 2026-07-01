using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APDS.Models;
using APDS.Events;

[Authorize(Roles = "Reviewer")]
public class ReviewController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly APDS.Services.AuditLogService _auditLog;
    private readonly APDS.Services.Notifications.INotificationPublisher _notificationPublisher;

    public ReviewController(ApplicationDbContext context, UserManager<User> userManager,
        APDS.Services.AuditLogService auditLog, APDS.Services.Notifications.INotificationPublisher notificationPublisher)
    {
        _context = context;
        _userManager = userManager;
        _auditLog = auditLog;
        _notificationPublisher = notificationPublisher;
    }

   public async Task<IActionResult> AcademicianActivities(string academicianId)
{
    var currentUser = await _userManager.GetUserAsync(User);

    bool isAssigned = await _context.ReviewerAssignments
        .AnyAsync(ra => ra.AcademicianId == academicianId && ra.ReviewerId == currentUser.Id);
    if (!isAssigned) return Forbid();

    var activities = await _context.Activities
        .Include(a => a.ActivityType)
        .Where(a => a.AcademicianId == academicianId
                 && a.DelegatedReviewerId == null
                 && (a.Status == ActivityStatus.SUBMITTED
                  || a.Status == ActivityStatus.UNDER_REVIEW
                  || a.Status == ActivityStatus.RESUBMITTED))
        .OrderBy(a => a.LastStatusChangeDate)
        .ToListAsync();

    var academician = await _userManager.FindByIdAsync(academicianId);
    var allReviewers = await _userManager.GetUsersInRoleAsync("Reviewer");

    var vm = new AcademicianActivitiesViewModel
    {
        AcademicianId = academicianId,
        AcademicianName = academician?.UserName ?? academicianId,
        Activities = activities,
        AvailableReviewers = allReviewers.Where(r => r.Id != currentUser.Id).ToList()
    };

    return View(vm);
}
   public async Task<IActionResult> ReviewActivity(int id)
{
    var user = await _userManager.GetUserAsync(User);

    var activity = await _context.Activities
        .Include(a => a.ActivityType)
        .Include(a => a.Academician)
        .FirstOrDefaultAsync(a => a.Id == id);

    if (activity == null) return NotFound();

    bool isAssigned = await _context.ReviewerAssignments
        .AnyAsync(ra => ra.AcademicianId == activity.AcademicianId && ra.ReviewerId == user.Id)
        && activity.DelegatedReviewerId == null;
    bool isDelegated = activity.DelegatedReviewerId == user.Id;
    if (!isAssigned && !isDelegated) return Forbid();

    ViewBag.IsDelegatedView = isDelegated;

    if (activity.Status == ActivityStatus.SUBMITTED || activity.Status == ActivityStatus.RESUBMITTED)
    {
        activity.Status = ActivityStatus.UNDER_REVIEW;
        await _context.SaveChangesAsync();
    }

    var existingReview = await _context.Reviews
        .FirstOrDefaultAsync(r => r.ActivityId == id && r.ReviewerId == user.Id);

    ViewBag.ExistingReview = existingReview;
    return View(activity);
}

    [HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SubmitReview(int activityId, ActivityStatus decision, string? comment)
{
    var user = await _userManager.GetUserAsync(User);

    var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == activityId);
    if (activity == null) return NotFound();

    bool isAssigned = await _context.ReviewerAssignments
        .AnyAsync(ra => ra.AcademicianId == activity.AcademicianId && ra.ReviewerId == user.Id)
        && activity.DelegatedReviewerId == null;
    bool isDelegated = activity.DelegatedReviewerId == user.Id;
    if (!isAssigned && !isDelegated) return Forbid();

        var validDecisions = new[] {
            ActivityStatus.APPROVED,
            ActivityStatus.REJECTED,
            ActivityStatus.REVISION_REQUESTED
        };

        if (!validDecisions.Contains(decision))
            return BadRequest("Geçersiz karar.");

        var existingReview = await _context.Reviews
            .FirstOrDefaultAsync(r => r.ActivityId == activityId && r.ReviewerId == user.Id);

        if (existingReview != null)
        {
            existingReview.Decision = decision;
            existingReview.Comment = comment;
            existingReview.ReviewDate = DateTime.UtcNow;
        }
        else
        {
            _context.Reviews.Add(new Review
            {
                ActivityId = activityId,
                ReviewerId = user.Id,
                Decision = decision,
                Comment = comment,
                ReviewDate = DateTime.UtcNow
            });
        }

        activity.Status = decision;
        activity.LastStatusChangeDate = DateTime.UtcNow;   // eksikti, eklendi
    
        await _context.SaveChangesAsync();
        await _auditLog.LogAsync("Review", activity.Id.ToString(), $"DECISION:{decision}", user.UserName);
        // ---- Bildirim: karar akademisyene gönderilir ----
        var message = decision switch
        {
            ActivityStatus.APPROVED => $"\"{activity.Title}\" başlıklı faaliyetiniz onaylandı.",
            ActivityStatus.REJECTED => $"\"{activity.Title}\" başlıklı faaliyetiniz reddedildi.",
            ActivityStatus.REVISION_REQUESTED => $"\"{activity.Title}\" başlıklı faaliyetiniz için revizyon talep edildi.",
            _ => $"\"{activity.Title}\" başlıklı faaliyetinizin durumu güncellendi."
        };  

        var notifType = decision switch
        {
            ActivityStatus.APPROVED => NotificationType.APPROVAL,
            ActivityStatus.REJECTED => NotificationType.REJECTION,
            ActivityStatus.REVISION_REQUESTED => NotificationType.REVISION_REQUESTED,
            _ => NotificationType.REMINDER
        };

        await _notificationPublisher.PublishAsync(new APDS.Events.ActivityStatusChangedEvent(
            RecipientUserId: activity.AcademicianId,
            Type: notifType,
            Message: message,
            RelatedEntityId: activity.Id
        ));


        TempData["Success"] = "Karar kaydedildi.";
        return RedirectToAction(nameof(AcademicianActivities), new { academicianId = activity.AcademicianId });
    }
[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Reviewer")]
public async Task<IActionResult> BulkReview(int[] activityIds, string decision, string? comment, string academicianId)
{
    if (activityIds == null || activityIds.Length == 0)
    {
        TempData["Error"] = "Hiçbir faaliyet seçilmedi.";
        return RedirectToAction(nameof(AcademicianActivities), new { academicianId });
    }

    var validDecisions = new Dictionary<string, ActivityStatus>
    {
        ["approve"] = ActivityStatus.APPROVED,
        ["reject"] = ActivityStatus.REJECTED,
        ["revision"] = ActivityStatus.REVISION_REQUESTED
    };

    if (!validDecisions.TryGetValue(decision, out var status))
        return BadRequest("Geçersiz karar.");

    if (status != ActivityStatus.APPROVED && string.IsNullOrWhiteSpace(comment))
    {
        TempData["Error"] = "Reddetme veya revizyon talebi için yorum zorunludur.";
        return RedirectToAction(nameof(AcademicianActivities), new { academicianId });
    }

    var user = await _userManager.GetUserAsync(User);

    bool isAssigned = await _context.ReviewerAssignments
        .AnyAsync(ra => ra.AcademicianId == academicianId && ra.ReviewerId == user.Id);
    if (!isAssigned) return Forbid();

    var activities = await _context.Activities
        .Where(a => activityIds.Contains(a.Id)
                 && a.AcademicianId == academicianId
                 && a.DelegatedReviewerId == null   // devredilmişler bu listede zaten görünmüyor, ek güvenlik
                 && (a.Status == ActivityStatus.SUBMITTED
                  || a.Status == ActivityStatus.UNDER_REVIEW
                  || a.Status == ActivityStatus.RESUBMITTED))
        .ToListAsync();

    foreach (var activity in activities)
    {
        var existingReview = await _context.Reviews
            .FirstOrDefaultAsync(r => r.ActivityId == activity.Id && r.ReviewerId == user.Id);

        if (existingReview != null)
        {
            existingReview.Decision = status;
            existingReview.Comment = comment;
            existingReview.ReviewDate = DateTime.UtcNow;
        }
        else
        {
            _context.Reviews.Add(new Review
            {
                ActivityId = activity.Id,
                ReviewerId = user.Id,
                Decision = status,
                Comment = comment,
                ReviewDate = DateTime.UtcNow
            });
        }

        activity.Status = status;
        activity.LastStatusChangeDate = DateTime.UtcNow;
        activity.OverdueNotificationSent = false;

        await _auditLog.LogAsync("Review", activity.Id.ToString(), $"BULK_DECISION:{status}", user.UserName);

        var message = status switch
        {
            ActivityStatus.APPROVED => $"\"{activity.Title}\" başlıklı faaliyetiniz onaylandı.",
            ActivityStatus.REJECTED => $"\"{activity.Title}\" başlıklı faaliyetiniz reddedildi.",
            ActivityStatus.REVISION_REQUESTED => $"\"{activity.Title}\" başlıklı faaliyetiniz için revizyon talep edildi.",
            _ => $"\"{activity.Title}\" başlıklı faaliyetinizin durumu güncellendi."
        };

        var notifType = status switch
        {
            ActivityStatus.APPROVED => NotificationType.APPROVAL,
            ActivityStatus.REJECTED => NotificationType.REJECTION,
            ActivityStatus.REVISION_REQUESTED => NotificationType.REVISION_REQUESTED,
            _ => NotificationType.REMINDER
        };

        await _notificationPublisher.PublishAsync(new ActivityStatusChangedEvent(
            RecipientUserId: activity.AcademicianId,
            Type: notifType,
            Message: message,
            RelatedEntityId: activity.Id
        ));
    }

    await _context.SaveChangesAsync();

    TempData["Success"] = $"{activities.Count} faaliyet için karar kaydedildi.";
    return RedirectToAction(nameof(AcademicianActivities), new { academicianId });
}
    
    [HttpPost]
    [ValidateAntiForgeryToken]
[Authorize(Roles = "Reviewer")]
public async Task<IActionResult> DelegateActivity(int activityId, string targetReviewerId)
{
    var activity = await _context.Activities.FindAsync(activityId);
    if (activity == null) return NotFound();

    var currentReviewerId = _userManager.GetUserId(User);

    bool isAssigned = _context.ReviewerAssignments
        .Any(ra => ra.AcademicianId == activity.AcademicianId && ra.ReviewerId == currentReviewerId);
    if (!isAssigned) return Forbid();

    if (targetReviewerId == currentReviewerId)
        return BadRequest("Kendine devredemezsin.");

    var target = await _userManager.FindByIdAsync(targetReviewerId);
    if (target == null || !await _userManager.IsInRoleAsync(target, "Reviewer"))
        return BadRequest("Geçersiz reviewer.");

    activity.PendingDelegationReviewerId = targetReviewerId;
    await _context.SaveChangesAsync();

    await _notificationPublisher.PublishAsync(new ActivityStatusChangedEvent(
        RecipientUserId: targetReviewerId,
        Type: NotificationType.DELEGATION_REQUEST,
        Message: $"{activity.Title} faaliyeti size devredilmek isteniyor.",
        RelatedEntityId: activity.Id
    ));

    await _auditLog.LogAsync(nameof(Activity), activity.Id.ToString(), "DELEGATE_REQUEST", currentReviewerId);

    return RedirectToAction("AcademicianActivities", new { academicianId = activity.AcademicianId });
}

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Reviewer")]
public async Task<IActionResult> AcceptDelegation(int activityId)
{
    var activity = await _context.Activities.FindAsync(activityId);
    var currentReviewerId = _userManager.GetUserId(User);

    if (activity == null || activity.PendingDelegationReviewerId != currentReviewerId)
        return Forbid();

    var originalReviewerId = _context.ReviewerAssignments
        .Where(ra => ra.AcademicianId == activity.AcademicianId)
        .Select(ra => ra.ReviewerId)
        .FirstOrDefault();

    activity.DelegatedReviewerId = currentReviewerId;
    activity.PendingDelegationReviewerId = null;
    await _context.SaveChangesAsync();

    if (originalReviewerId != null)
        await _notificationPublisher.PublishAsync(new ActivityStatusChangedEvent(
            RecipientUserId: originalReviewerId,
            Type: NotificationType.DELEGATION_ACCEPTED,
            Message: $"{activity.Title} devir teklifin kabul edildi.",
            RelatedEntityId: activity.Id
        ));

    await _auditLog.LogAsync(nameof(Activity), activity.Id.ToString(), "DELEGATE_ACCEPT", currentReviewerId);

    return RedirectToAction("ReviewerPaneli", "Home");
}

[HttpPost]
[ValidateAntiForgeryToken]
[Authorize(Roles = "Reviewer")]
public async Task<IActionResult> RejectDelegation(int activityId)
{
    var activity = await _context.Activities.FindAsync(activityId);
    var currentReviewerId = _userManager.GetUserId(User);

    if (activity == null || activity.PendingDelegationReviewerId != currentReviewerId)
        return Forbid();

    var originalReviewerId = _context.ReviewerAssignments
        .Where(ra => ra.AcademicianId == activity.AcademicianId)
        .Select(ra => ra.ReviewerId)
        .FirstOrDefault();

    activity.PendingDelegationReviewerId = null;
    await _context.SaveChangesAsync();

    if (originalReviewerId != null)
        await _notificationPublisher.PublishAsync(new ActivityStatusChangedEvent(
            RecipientUserId: originalReviewerId,
            Type: NotificationType.DELEGATION_REJECTED,
            Message: $"{activity.Title} devir teklifin reddedildi.",
            RelatedEntityId: activity.Id
        ));

    return RedirectToAction("ReviewerPaneli", "Home");
}
}