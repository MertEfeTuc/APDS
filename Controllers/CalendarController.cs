using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APDS.Models;

namespace APDS.Controllers
{
    [Authorize(Roles = "Akademisyen,Reviewer")]
    public class CalendarController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CalendarController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public IActionResult Index()
        {
            return View();
        }

        // FullCalendar bu endpoint'i otomatik çağırır, görünür aralığı start/end ile gönderir
        [HttpGet]
        public async Task<IActionResult> Events()
        {
            var user = await _userManager.GetUserAsync(User);

            IQueryable<Activity> query;

            if (User.IsInRole("Akademisyen"))
            {
                query = _context.Activities
                    .Where(a => a.AcademicianId == user.Id);
            }
            else // Reviewer
            {
                var assignedAcademicianIds = await _context.ReviewerAssignments
                    .Where(r => r.ReviewerId == user.Id)
                    .Select(r => r.AcademicianId)
                    .ToListAsync();

                query = _context.Activities
                    .Where(a => assignedAcademicianIds.Contains(a.AcademicianId));
            }

            var activities = await query
                .Include(a => a.ActivityType)
                .Include(a => a.Academician)
                .ToListAsync();

            var events = activities.Select(a => new
            {
                id = a.Id,
                title = User.IsInRole("Reviewer")
                    ? $"{a.Title} ({a.Academician.UserName})" // gerçek isim property'ne göre düzelt
                    : a.Title,
                start = a.ActivityDate.ToString("yyyy-MM-dd"),
                allDay = true,
                color = GetColorForStatus(a.Status),
                url = Url.Action("Details", "Activity", new { id = a.Id })
            });

            return Json(events);
        }

        private static string GetColorForStatus(ActivityStatus status) => status switch
        {
            ActivityStatus.DRAFT => "#9e9e9e",
            ActivityStatus.SUBMITTED => "#2196f3",
            ActivityStatus.RESUBMITTED => "#2196f3",
            ActivityStatus.UNDER_REVIEW => "#ff9800",
            ActivityStatus.APPROVED => "#4caf50",
            ActivityStatus.REJECTED => "#f44336",
            ActivityStatus.REVISION_REQUESTED => "#e91e63",
            _ => "#607d8b"
        };
    }
}