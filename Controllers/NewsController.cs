using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APDS.Controllers
{
    [Authorize(Roles = "Akademisyen,Reviewer,Admin")]
    public class NewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.NewsItems
                .Include(ni => ni.NewsSource)
                .OrderByDescending(ni => ni.PublishedDate ?? ni.FetchedDate)
                .Take(100)
                .ToListAsync();

            return View(items);
        }
    }
}
