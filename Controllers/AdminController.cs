using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Documentshare.Data;
using Documentshare.Models;

namespace Documentshare.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _ctx;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext ctx, IWebHostEnvironment env)
        {
            _ctx = ctx;
            _env = env;
        }

        private bool IsAdmin()
        {
            var role = HttpContext.Session.GetString("UserRole");
            return role == "Admin";
        }

        private IActionResult RedirectIfNotAdmin()
        {
            TempData["ErrorMessage"] = "Bạn không có quyền truy cập khu vực quản trị.";
            return RedirectToAction("Login", "Account");
        }

        public async Task<IActionResult> Index()
        {
            if (!IsAdmin()) return RedirectIfNotAdmin();

            var pending        = await _ctx.Documents.Include(d => d.Category).Where(d => !d.IsApproved && d.ParentId == null).OrderByDescending(d => d.UploadDate).ToListAsync();
            var totalDocs      = await _ctx.Documents.CountAsync(d => d.ParentId == null);
            var totalApproved  = await _ctx.Documents.CountAsync(d => d.IsApproved && d.ParentId == null);
            var totalUsers     = await _ctx.Users.CountAsync();
            var totalAdmins    = await _ctx.Users.CountAsync(u => u.Role == "Admin");
            var totalDownloads = await _ctx.Documents.SumAsync(d => (int?)d.DownloadCount) ?? 0;
            var totalViews     = await _ctx.Documents.SumAsync(d => (int?)d.ViewCount) ?? 0;

            ViewBag.TotalDocs      = totalDocs;
            ViewBag.TotalApproved  = totalApproved;
            ViewBag.TotalPending   = pending.Count;
            ViewBag.TotalUsers     = totalUsers;
            ViewBag.TotalAdmins    = totalAdmins;
            ViewBag.TotalDownloads = totalDownloads;
            ViewBag.TotalViews     = totalViews;

            return View(pending);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            if (!IsAdmin()) return Unauthorized();
            var doc = await _ctx.Documents.Include(d => d.SubDocuments).FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            
            doc.IsApproved = true;
            if (doc.SubDocuments != null)
            {
                foreach (var sub in doc.SubDocuments)
                {
                    sub.IsApproved = true;
                }
            }
            
            await _ctx.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Đã phê duyệt tài liệu «{doc.Title}».";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            if (!IsAdmin()) return Unauthorized();
            var doc = await _ctx.Documents.Include(d => d.SubDocuments).FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            try
            {
                if (!string.IsNullOrEmpty(doc.FilePath) && !doc.FilePath.StartsWith("http"))
                {
                    var fp = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(fp)) System.IO.File.Delete(fp);
                }
                
                _ctx.Documents.Remove(doc);
                await _ctx.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã từ chối và xóa tài liệu «{doc.Title}».";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xóa: " + ex.Message;
            }
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Users()
        {
            if (!IsAdmin()) return RedirectIfNotAdmin();
            var users = await _ctx.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();
            return View(users);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SetRole(int userId, string role)
        {
            if (!IsAdmin()) return Unauthorized();

            var currentId = HttpContext.Session.GetInt32("UserId");
            if (currentId == userId)
            {
                TempData["ErrorMessage"] = "Bạn không thể tự thay đổi vai trò của chính mình.";
                return RedirectToAction(nameof(Users));
            }

            if (role != "User" && role != "Admin")
            {
                TempData["ErrorMessage"] = "Vai trò không hợp lệ.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _ctx.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.Role = role;
            await _ctx.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã đổi vai trò của «{user.DisplayName}» thành {(role == "Admin" ? "Quản trị viên" : "Thành viên")}.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUser(int userId)
        {
            if (!IsAdmin()) return Unauthorized();

            var currentId = HttpContext.Session.GetInt32("UserId");
            if (currentId == userId)
            {
                TempData["ErrorMessage"] = "Không thể khóa tài khoản của chính bạn.";
                return RedirectToAction(nameof(Users));
            }

            var user = await _ctx.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _ctx.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Tài khoản «{user.DisplayName}» đã được {(user.IsActive ? "kích hoạt" : "khóa")}.";
            return RedirectToAction(nameof(Users));
        }

        public async Task<IActionResult> Documents()
        {
            if (!IsAdmin()) return RedirectIfNotAdmin();
            var docs = await _ctx.Documents
                .Include(d => d.Category)
                .Where(d => d.ParentId == null)
                .OrderByDescending(d => d.UploadDate)
                .ToListAsync();
            return View(docs);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDocument(int id)
        {
            if (!IsAdmin()) return Unauthorized();
            var doc = await _ctx.Documents.Include(d => d.SubDocuments).FirstOrDefaultAsync(d => d.Id == id);
            if (doc == null) return NotFound();
            try
            {
                if (!string.IsNullOrEmpty(doc.FilePath) && !doc.FilePath.StartsWith("http"))
                {
                    var fp = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (System.IO.File.Exists(fp)) System.IO.File.Delete(fp);
                }
                
                _ctx.Documents.Remove(doc);
                await _ctx.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Đã xóa tài liệu «{doc.Title}».";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi: " + ex.Message;
            }
            return RedirectToAction(nameof(Documents));
        }
        // Trang cài đặt thông tin footer trang web
        public async Task<IActionResult> SiteSettings()
        {
            if (!IsAdmin()) return RedirectIfNotAdmin();

            // Lấy cài đặt hiện tại, nếu chưa có thì tạo mới
            var settings = await _ctx.SiteSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new SiteSettings();
                _ctx.SiteSettings.Add(settings);
                await _ctx.SaveChangesAsync();
            }
            return View(settings);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveSiteSettings(SiteSettings model)
        {
            if (!IsAdmin()) return Unauthorized();
            if (!ModelState.IsValid) return View("SiteSettings", model);

            var settings = await _ctx.SiteSettings.FirstOrDefaultAsync();
            if (settings == null)
            {
                // Nếu chưa có bản ghi nào, tạo mới
                settings = new SiteSettings();
                _ctx.SiteSettings.Add(settings);
            }

            // Cập nhật từng trường thông tin footer
            settings.SiteName        = model.SiteName?.Trim() ?? settings.SiteName;
            settings.SiteDescription = model.SiteDescription?.Trim() ?? settings.SiteDescription;
            settings.FooterEmail     = model.FooterEmail?.Trim() ?? settings.FooterEmail;
            settings.FooterAddress   = model.FooterAddress?.Trim() ?? settings.FooterAddress;
            settings.CopyrightText   = model.CopyrightText?.Trim() ?? settings.CopyrightText;
            settings.FooterTagline   = model.FooterTagline?.Trim() ?? settings.FooterTagline;

            await _ctx.SaveChangesAsync();
            TempData["SuccessMessage"] = "Đã lưu thông tin footer trang web thành công!";
            return RedirectToAction(nameof(SiteSettings));
        }
    }
}
