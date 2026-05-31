using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Documentshare.Data;
using Documentshare.Models;

namespace Documentshare.Controllers
{
    public class DocumentController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _env;

        public DocumentController(AppDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        // Browse all documents (separate full-list page)
        public async Task<IActionResult> Browse(string? q, int? categoryId, string? level, string? ext, string sort = "newest")
        {
            var categories = await _context.Categories.ToListAsync();

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

            query = sort switch
            {
                "downloads" => query.OrderByDescending(d => d.DownloadCount),
                "views"     => query.OrderByDescending(d => d.ViewCount),
                "oldest"    => query.OrderBy(d => d.UploadDate),
                _           => query.OrderByDescending(d => d.UploadDate)
            };

            ViewBag.Documents  = await query.ToListAsync();
            ViewBag.Categories = categories;
            ViewBag.Q          = q;
            ViewBag.CategoryId = categoryId;
            ViewBag.Level      = level;
            ViewBag.Ext        = ext;
            ViewBag.Sort       = sort;
            return View();
        }

        // Document details
        public async Task<IActionResult> Details(int id)
        {
            var doc = await _context.Documents
                .Include(d => d.Category)
                .Include(d => d.Comments)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null) return NotFound();

            // Only increment view for approved docs
            if (doc.IsApproved)
            {
                doc.ViewCount++;
                await _context.SaveChangesAsync();
            }

            doc.Comments = doc.Comments.OrderByDescending(c => c.CreatedDate).ToList();
            return View(doc);
        }

        // Download file
        public async Task<IActionResult> Download(int id)
        {
            var doc = await _context.Documents.FindAsync(id);
            if (doc == null) return NotFound();

            var path = Path.Combine(_env.WebRootPath, doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(path))
                return NotFound("File không tồn tại trên máy chủ.");

            doc.DownloadCount++;
            await _context.SaveChangesAsync();

            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(path, out var ct)) ct = "application/octet-stream";

            var bytes = await System.IO.File.ReadAllBytesAsync(path);
            return File(bytes, ct, doc.OriginalFileName);
        }

        // Upload form (GET)
        [HttpGet]
        public async Task<IActionResult> Upload()
        {
            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.UploadType = "file";
            return View();
        }

        private string? ConvertGoogleDriveLinkToEmbed(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();

            // Handle standard /file/d/FILE_ID/view
            if (url.Contains("/file/d/"))
            {
                try
                {
                    var startIndex = url.IndexOf("/file/d/") + "/file/d/".Length;
                    var endIndex = url.IndexOf("/", startIndex);
                    if (endIndex == -1) endIndex = url.IndexOf("?", startIndex);
                    if (endIndex == -1) endIndex = url.Length;
                    
                    var fileId = url.Substring(startIndex, endIndex - startIndex);
                    if (!string.IsNullOrEmpty(fileId))
                    {
                        return $"https://drive.google.com/file/d/{fileId}/preview";
                    }
                }
                catch { }
            }

            // Handle ?id=FILE_ID
            if (url.Contains("id="))
            {
                try
                {
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"[?&]id=([^&]+)");
                    if (match.Success)
                    {
                        return $"https://drive.google.com/file/d/{match.Groups[1].Value}/preview";
                    }
                }
                catch { }
            }

            // If it is already an embed preview link
            if (url.Contains("/file/d/") && url.Contains("/preview"))
            {
                return url;
            }

            return null;
        }

        // Upload form (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(Document model, IFormFile? file, string? uploadType, string? driveLink)
        {
            // Remove server-generated fields from model validation
            ModelState.Remove(nameof(model.FilePath));
            ModelState.Remove(nameof(model.OriginalFileName));
            ModelState.Remove(nameof(model.FileExtension));
            ModelState.Remove(nameof(model.FileSize));

            if (uploadType == "link")
            {
                if (string.IsNullOrWhiteSpace(driveLink))
                {
                    ModelState.AddModelError("driveLink", "Vui lòng nhập đường dẫn video Google Drive.");
                }
                else
                {
                    var embedUrl = ConvertGoogleDriveLinkToEmbed(driveLink);
                    if (string.IsNullOrEmpty(embedUrl))
                    {
                        ModelState.AddModelError("driveLink", "Đường dẫn Google Drive không hợp lệ. Vui lòng nhập link dạng: https://drive.google.com/file/d/FILE_ID/view");
                    }
                    else
                    {
                        model.FilePath = embedUrl;
                        model.OriginalFileName = "Google Drive Video";
                        model.FileExtension = ".gdrive";
                        model.FileSize = 0;
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
                ViewBag.UploadType = uploadType;
                ViewBag.DriveLink = driveLink;
                return View(model);
            }

            try
            {
                var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
                Directory.CreateDirectory(uploadsDir);

                if (uploadType != "link")
                {
                    if (file != null && file.Length > 0)
                    {
                        var ext  = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var name = Guid.NewGuid().ToString("N") + ext;
                        var dest = Path.Combine(uploadsDir, name);
                        await using var fs = new FileStream(dest, FileMode.Create);
                        await file.CopyToAsync(fs);

                        model.FilePath        = "/uploads/" + name;
                        model.OriginalFileName = file.FileName;
                        model.FileSize        = file.Length;
                        model.FileExtension   = ext;
                    }
                    else
                    {
                        // No file selected — create a placeholder text file
                        var name = Guid.NewGuid().ToString("N") + ".txt";
                        var dest = Path.Combine(uploadsDir, name);
                        await System.IO.File.WriteAllTextAsync(dest,
                            $"Tài liệu: {model.Title}\nTác giả: {model.Author}\nMô tả: {model.Description}");
                        model.FilePath        = "/uploads/" + name;
                        model.OriginalFileName = model.Title + ".txt";
                        model.FileSize        = 512;
                        model.FileExtension   = ".txt";
                    }
                }

                model.UploadDate = DateTime.Now;
                model.IsApproved = false;
                _context.Add(model);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Tài liệu đã được gửi lên thành công và đang chờ Admin phê duyệt.";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "Lỗi khi lưu tệp: " + ex.Message);
                ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
                ViewBag.UploadType = uploadType;
                ViewBag.DriveLink = driveLink;
                return View(model);
            }
        }

        // Add comment (AJAX POST)
        [HttpPost]
        public async Task<IActionResult> AddComment(int documentId, string author, string content, int rating)
        {
            if (string.IsNullOrWhiteSpace(author) || string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin." });

            var doc = await _context.Documents.FindAsync(documentId);
            if (doc == null) return Json(new { success = false, message = "Không tìm thấy tài liệu." });

            var comment = new Comment
            {
                DocumentId  = documentId,
                Author      = author.Trim(),
                Content     = content.Trim(),
                Rating      = Math.Clamp(rating, 1, 5),
                CreatedDate = DateTime.Now
            };
            _context.Comments.Add(comment);
            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                author  = comment.Author,
                content = comment.Content,
                rating  = comment.Rating,
                date    = comment.CreatedDate.ToString("dd/MM/yyyy HH:mm")
            });
        }
    }
}
