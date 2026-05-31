using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Documentshare.Data;
using Documentshare.Models;

namespace Documentshare.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;
        private readonly ILogger<HomeController> _logger;

        public HomeController(AppDbContext context, ILogger<HomeController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(string? q, int? categoryId, string? level, string? ext, string? lang, string sort = "newest")
        {
            var categories = await _context.Categories.ToListAsync();
            var approvedQ   = _context.Documents.Where(d => d.IsApproved);

            var vm = new HomeViewModel
            {
                Categories      = categories,
                TotalDocuments  = await approvedQ.CountAsync(),
                TotalDownloads  = await approvedQ.SumAsync(d => (int?)d.DownloadCount) ?? 0,
                TotalViews      = await approvedQ.SumAsync(d => (int?)d.ViewCount)     ?? 0,
                TotalCategories = categories.Count,
                SearchString    = q     ?? string.Empty,
                SelectedCategoryId = categoryId,
                SelectedLevel   = level ?? string.Empty,
                SelectedExtension = ext ?? string.Empty,
                SelectedLanguage  = lang ?? string.Empty,
                SortBy          = sort,
                FeaturedDocuments = await approvedQ
                    .Include(d => d.Category)
                    .OrderByDescending(d => d.DownloadCount)
                    .Take(3).ToListAsync()
            };

            // Build main document query
            var query = _context.Documents
                .Include(d => d.Category)
                .Include(d => d.Comments)
                .Where(d => d.IsApproved);

            if (!string.IsNullOrWhiteSpace(q))
            {
                var ql = q.ToLower();
                query = query.Where(d =>
                    d.Title.ToLower().Contains(ql) ||
                    d.Description.ToLower().Contains(ql) ||
                    d.Tags.ToLower().Contains(ql) ||
                    d.Author.ToLower().Contains(ql));
            }
            if (categoryId.HasValue && categoryId.Value > 0)
                query = query.Where(d => d.CategoryId == categoryId.Value);
            if (!string.IsNullOrEmpty(level))
                query = query.Where(d => d.Level == level);
            if (!string.IsNullOrEmpty(ext))
                query = query.Where(d => d.FileExtension.ToLower() == ext.ToLower());
            if (!string.IsNullOrEmpty(lang))
                query = query.Where(d => d.Language == lang);

            query = sort switch
            {
                "downloads" => query.OrderByDescending(d => d.DownloadCount),
                "views"     => query.OrderByDescending(d => d.ViewCount),
                "oldest"    => query.OrderBy(d => d.UploadDate),
                _           => query.OrderByDescending(d => d.UploadDate)
            };

            vm.Documents = await query.ToListAsync();
            return View(vm);
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
