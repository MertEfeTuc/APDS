using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APDS.Models;

namespace APDS.Controllers
{
    [Authorize(Roles = "Akademisyen,Reviewer,Admin")]
    public class ComparisonController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public ComparisonController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> DepartmentFacultyComparison()
        {
            var academicianUserIds = await _userManager.GetUsersInRoleAsync("Akademisyen");
            var academicianIds = academicianUserIds.Select(u => u.Id).ToHashSet();

            var academicians = await _context.Users
                .Where(u => academicianIds.Contains(u.Id) && u.DepartmentId != null)
                .Include(u => u.Department)
                .ThenInclude(d => d.Faculty)
                .ToListAsync();

            var approvedScores = await _context.Activities
                .Include(a => a.ActivityType)
                .Where(a => a.Status == ActivityStatus.APPROVED)
                .Select(a => new { a.AcademicianId, a.ActivityType.Score })
                .ToListAsync();

            var scoreByUser = approvedScores
                .GroupBy(a => a.AcademicianId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Score));

            var academicianTotals = academicians.Select(u => new
            {
                u.Id,
                DepartmentId = u.DepartmentId!.Value,
                DepartmentName = u.Department.Name,
                FacultyId = u.Department.FacultyId,
                FacultyName = u.Department.Faculty.Name,
                TotalScore = scoreByUser.GetValueOrDefault(u.Id, 0)
            }).ToList();

            var departmentStats = academicianTotals
                .GroupBy(a => new { a.DepartmentId, a.DepartmentName, a.FacultyId, a.FacultyName })
                .Select(g => new
                {
                    g.Key.DepartmentId,
                    g.Key.DepartmentName,
                    g.Key.FacultyId,
                    g.Key.FacultyName,
                    AverageScore = Math.Round(g.Average(x => x.TotalScore), 2),
                    AcademicianCount = g.Count()
                })
                .OrderByDescending(d => d.AverageScore)
                .ToList();

            var facultyStats = academicianTotals
                .GroupBy(a => new { a.FacultyId, a.FacultyName })
                .Select(g => new
                {
                    g.Key.FacultyId,
                    g.Key.FacultyName,
                    AverageScore = Math.Round(g.Average(x => x.TotalScore), 2),
                    AcademicianCount = g.Count()
                })
                .OrderByDescending(f => f.AverageScore)
                .ToList();

            int? myDepartmentId = null;
            int? myFacultyId = null;

            if (User.IsInRole("Akademisyen"))
            {
                var me = await _userManager.GetUserAsync(User);
                myDepartmentId = me.DepartmentId;
                myFacultyId = academicianTotals.FirstOrDefault(a => a.Id == me.Id)?.FacultyId;
            }

            ViewBag.DepartmentStats = departmentStats;
            ViewBag.FacultyStats = facultyStats;
            ViewBag.MyDepartmentId = myDepartmentId;
            ViewBag.MyFacultyId = myFacultyId;

            return View();
        }
    }
}