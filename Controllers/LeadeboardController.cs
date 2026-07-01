using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APDS.Models;

namespace APDS.Controllers
{
    [Authorize(Roles = "Akademisyen,Reviewer,Admin")]
    public class LeaderboardController : Controller
    {
        private const int TopN = 20;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public LeaderboardController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int? year)
        {
            var academicianUserIds = (await _userManager.GetUsersInRoleAsync("Akademisyen"))
                .Select(u => u.Id)
                .ToHashSet();

            var academicians = await _context.Users
                .Where(u => academicianUserIds.Contains(u.Id))
                .Include(u => u.Department)
                .ToListAsync();

            var approvedQuery = _context.Activities
                .Include(a => a.ActivityType)
                .Where(a => a.Status == ActivityStatus.APPROVED);

            if (year.HasValue)
            {
                approvedQuery = approvedQuery.Where(a => a.ActivityDate.Year == year.Value);
            }

            var approved = await approvedQuery
                .Select(a => new { a.AcademicianId, a.ActivityType.Score })
                .ToListAsync();

            var scoreByUser = approved
                .GroupBy(a => a.AcademicianId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Score));

            var ranked = academicians
                .Select(u => new
                {
                    u.Id,
                    Name = u.UserName, // gerçek property adınla eşleştir (örn. u.FirstName + " " + u.LastName)
                    DepartmentName = u.Department != null ? u.Department.Name : "-",
                    TotalScore = scoreByUser.GetValueOrDefault(u.Id, 0)
                })
                .OrderByDescending(x => x.TotalScore)
                .ToList();

            var rankedWithPosition = ranked
                .Select((x, i) => new
                {
                    x.Id,
                    x.Name,
                    x.DepartmentName,
                    x.TotalScore,
                    Rank = i + 1
                })
                .ToList();

            var topList = rankedWithPosition.Take(TopN).ToList();

            object myRow = null;
            if (User.IsInRole("Akademisyen"))
            {
                var me = await _userManager.GetUserAsync(User);
                var alreadyInTop = topList.Any(x => x.Id == me.Id);
                if (!alreadyInTop)
                {
                    myRow = rankedWithPosition.FirstOrDefault(x => x.Id == me.Id);
                }
            }

            var availableYears = await _context.Activities
                .Where(a => a.Status == ActivityStatus.APPROVED)
                .Select(a => a.ActivityDate.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            ViewBag.TopList = topList;
            ViewBag.MyRow = myRow;
            ViewBag.SelectedYear = year;
            ViewBag.AvailableYears = availableYears;

            return View();
        }
    }
}