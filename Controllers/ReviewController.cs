using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APDS.Models;

[Authorize(Roles = "Reviewer")]
public class ReviewController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly APDS.Services.AuditLogService _auditLog;
    public ReviewController(ApplicationDbContext context, UserManager<User> userManager, APDS.Services.AuditLogService auditLog)
    {
        _context = context;
        _userManager = userManager;
        _auditLog = auditLog;
    }

    public async Task<IActionResult> AcademicianActivities(string academicianId)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        var assignment = await _context.ReviewerAssignments
            .FirstOrDefaultAsync(a => a.ReviewerId == currentUser.Id
                                   && a.AcademicianId == academicianId);

        if (assignment == null) return Forbid();

        var activities = await _context.Activities
            .Include(a => a.ActivityType)
            .Where(a => a.AcademicianId == academicianId
                     && (a.Status == ActivityStatus.SUBMITTED
                      || a.Status == ActivityStatus.UNDER_REVIEW
                      || a.Status == ActivityStatus.RESUBMITTED))
            .OrderByDescending(a => a.CreatedDate)
            .ToListAsync();

        var academician = await _userManager.FindByIdAsync(academicianId);
        ViewBag.AcademicianName = academician?.UserName ?? academicianId;
        ViewBag.AcademicianId = academicianId;

        return View(activities);
    }

    public async Task<IActionResult> ReviewActivity(int id)
    {
        var user = await _userManager.GetUserAsync(User);

        var activity = await _context.Activities
            .Include(a => a.ActivityType)
            .Include(a => a.Academician)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null) return NotFound();

        var assignment = await _context.ReviewerAssignments
            .FirstOrDefaultAsync(a => a.ReviewerId == user.Id
                                   && a.AcademicianId == activity.AcademicianId);

        if (assignment == null) return Forbid();
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

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId);

        if (activity == null) return NotFound();

        var assignment = await _context.ReviewerAssignments
            .FirstOrDefaultAsync(a => a.ReviewerId == user.Id
                                   && a.AcademicianId == activity.AcademicianId);

        if (assignment == null) return Forbid();

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
        await _context.SaveChangesAsync();
        await _auditLog.LogAsync("Review", activity.Id.ToString(), $"DECISION:{decision}", user.UserName);
        TempData["Success"] = "Karar kaydedildi.";
        return RedirectToAction(nameof(AcademicianActivities), new { academicianId = activity.AcademicianId });
    }
}