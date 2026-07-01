using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APDS.Models;

public class HomeController : Controller
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;

    public HomeController(UserManager<User> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

public async Task<IActionResult> Index()
{
    var user = await _userManager.GetUserAsync(User);

    if (user == null)
        return RedirectToAction("Login", "Account");

    if (await _userManager.IsInRoleAsync(user, "Akademisyen"))
        return RedirectToAction("AkademisyenPaneli");

    if (await _userManager.IsInRoleAsync(user, "Reviewer"))
        return RedirectToAction("ReviewerPaneli");

    if (await _userManager.IsInRoleAsync(user, "Admin"))
        return RedirectToAction("Index", "Admin");   

    return RedirectToAction("Login", "Account");
}
    [Authorize(Roles = "Akademisyen")]
public async Task<IActionResult> AkademisyenPaneli()
{
    var user = await _userManager.GetUserAsync(User);

    var activities = await _context.Activities
        .Include(a => a.ActivityType)
        .Where(a => a.AcademicianId == user.Id)
        .ToListAsync();

    // Tüm zamanlar toplam puan (sadece APPROVED)
    var myTotalScore = activities
        .Where(a => a.Status == ActivityStatus.APPROVED)
        .Sum(a => a.ActivityType?.Score ?? 0);

    // Departman sıralaması
    if (user.DepartmentId != null)
    {
        // Aynı departmandaki tüm akademisyenler ve puanları
        var deptUsers = await _context.Users
            .Where(u => u.DepartmentId == user.DepartmentId)
            .ToListAsync();

        var deptScores = new List<(string UserId, int Score)>();
        foreach (var u in deptUsers)
        {
            var score = await _context.Activities
                .Where(a => a.AcademicianId == u.Id && a.Status == ActivityStatus.APPROVED)
                .Include(a => a.ActivityType)
                .SumAsync(a => a.ActivityType != null ? a.ActivityType.Score : 0);
            deptScores.Add((u.Id, score));
        }

        var deptRanked = deptScores.OrderByDescending(x => x.Score).ToList();
        var deptRank = deptRanked.FindIndex(x => x.UserId == user.Id) + 1;

        ViewBag.DeptRank = deptRank;
        ViewBag.DeptTotal = deptUsers.Count;

        // Fakülte sıralaması
        var dept = await _context.Departments
            .Include(d => d.Faculty)
            .ThenInclude(f => f.Departments)
            .FirstOrDefaultAsync(d => d.Id == user.DepartmentId);

        if (dept?.Faculty != null)
        {
            var facultyDeptIds = dept.Faculty.Departments.Select(d => d.Id).ToList();

            var facultyUsers = await _context.Users
                .Where(u => u.DepartmentId != null && facultyDeptIds.Contains(u.DepartmentId.Value))
                .ToListAsync();

            var facultyScores = new List<(string UserId, int Score)>();
            foreach (var u in facultyUsers)
            {
                var score = await _context.Activities
                    .Where(a => a.AcademicianId == u.Id && a.Status == ActivityStatus.APPROVED)
                    .Include(a => a.ActivityType)
                    .SumAsync(a => a.ActivityType != null ? a.ActivityType.Score : 0);
                facultyScores.Add((u.Id, score));
            }

            var facultyRanked = facultyScores.OrderByDescending(x => x.Score).ToList();
            var facultyRank = facultyRanked.FindIndex(x => x.UserId == user.Id) + 1;

            ViewBag.FacultyRank = facultyRank;
            ViewBag.FacultyTotal = facultyUsers.Count;
            ViewBag.FacultyName = dept.Faculty.Name;
            ViewBag.DeptName = dept.Name;
        }
    }

    return View("~/Views/Activity/Index.cshtml", activities);
}

[Authorize(Roles = "Reviewer")]
public async Task<IActionResult> ReviewerPaneli()
{
    var reviewer = await _userManager.GetUserAsync(User);

    var academicians = await _context.ReviewerAssignments
        .Include(ra => ra.Academician)
        .Where(ra => ra.ReviewerId == reviewer.Id)
        .Select(ra => ra.Academician)
        .ToListAsync();

    // Her akademisyenin bekleyen faaliyet sayısını hesapla
    var pendingCounts = new Dictionary<string, int>();
    foreach (var academician in academicians)
    {
        var count = await _context.Activities
            .CountAsync(a => a.AcademicianId == academician.Id &&
                              (a.Status == ActivityStatus.SUBMITTED || a.Status == ActivityStatus.RESUBMITTED));
        pendingCounts[academician.Id] = count;
    }

    ViewBag.PendingCounts = pendingCounts;
    ViewBag.TotalReviewed = await _context.Reviews.CountAsync(r => r.ReviewerId == reviewer.Id);

    return View(academicians);
}
[Authorize(Roles = "Reviewer")]
public async Task<IActionResult> ReviewerStatistics()
{
    var reviewer = await _userManager.GetUserAsync(User);

    var academicians = await _context.ReviewerAssignments
        .Include(ra => ra.Academician)
        .Where(ra => ra.ReviewerId == reviewer.Id)
        .Select(ra => ra.Academician)
        .ToListAsync();
    var assignedAcademicianIds = academicians.Select(a => a.Id).ToList();
    var pendingCounts = new Dictionary<string, int>();
    foreach (var academician in academicians)
    {
        var count = await _context.Activities
            .CountAsync(a => a.AcademicianId == academician.Id
                           && a.DelegatedReviewerId == null
                           && (a.Status == ActivityStatus.SUBMITTED || a.Status == ActivityStatus.RESUBMITTED));
        pendingCounts[academician.Id] = count;
    }

    ViewBag.PendingCounts = pendingCounts;
    ViewBag.TotalReviewed = await _context.Reviews.CountAsync(r => r.ReviewerId == reviewer.Id);

    ViewBag.PendingDelegationOffers = await _context.Activities
        .Include(a => a.Academician)
        .Where(a => a.PendingDelegationReviewerId == reviewer.Id)
        .ToListAsync();

    ViewBag.DelegatedToMe = await _context.Activities
        .Include(a => a.Academician)
        .Include(a => a.ActivityType)
        .Where(a => a.DelegatedReviewerId == reviewer.Id)
        .OrderBy(a => a.LastStatusChangeDate)
        .ToListAsync();
        
var pendingCount = await _context.Activities
    .CountAsync(a => (assignedAcademicianIds.Contains(a.AcademicianId) && a.DelegatedReviewerId == null
                    || a.DelegatedReviewerId == reviewer.Id)
                   && (a.Status == ActivityStatus.SUBMITTED
                    || a.Status == ActivityStatus.RESUBMITTED
                    || a.Status == ActivityStatus.UNDER_REVIEW));

var approvedCount = await _context.Reviews
    .CountAsync(r => r.ReviewerId == reviewer.Id && r.Decision == ActivityStatus.APPROVED);

var rejectedCount = await _context.Reviews
    .CountAsync(r => r.ReviewerId == reviewer.Id && r.Decision == ActivityStatus.REJECTED);

var revisionCount = await _context.Reviews
    .CountAsync(r => r.ReviewerId == reviewer.Id && r.Decision == ActivityStatus.REVISION_REQUESTED);

ViewBag.PendingCount = pendingCount;
ViewBag.ApprovedCount = approvedCount;
ViewBag.RejectedCount = rejectedCount;
ViewBag.RevisionCount = revisionCount;
    return View(academicians);
}
}
