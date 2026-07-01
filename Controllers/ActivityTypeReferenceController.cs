using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APDS.Controllers
{
    [Authorize(Roles = "Akademisyen,Reviewer,Admin")]
    public class ActivityTypeReferenceController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ActivityTypeReferenceController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
{
    var types = await _context.ActivityTypes
        .OrderBy(t => t.Category)
        .ThenByDescending(t => t.Score)
        .ToListAsync();

    var counts = await _context.Activities
        .Where(a => a.Status == Models.ActivityStatus.APPROVED)
        .GroupBy(a => a.ActivityTypeId)
        .Select(g => new { ActivityTypeId = g.Key, Count = g.Count() })
        .ToDictionaryAsync(x => x.ActivityTypeId, x => x.Count);

    ViewBag.Counts = counts;

    return View(types);
}
    }
}