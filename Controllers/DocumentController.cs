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
        public async Task<IActionResult> Details(int id, int? subId)
        {
            var doc = await _context.Documents
                .Include(d => d.Category)
                .Include(d => d.Comments)
                .Include(d => d.SubDocuments)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (doc == null) return NotFound();

            // If this is a child document, redirect to its parent page and play this child
            if (doc.ParentId.HasValue)
            {
                return RedirectToAction(nameof(Details), new { id = doc.ParentId.Value, subId = doc.Id });
            }

            // Only increment view for approved docs
            if (doc.IsApproved)
            {
                doc.ViewCount++;
                await _context.SaveChangesAsync();
            }

            // Order subdocuments chronologically (by upload date)
            if (doc.SubDocuments != null && doc.SubDocuments.Count > 0)
            {
                doc.SubDocuments = doc.SubDocuments.OrderBy(d => d.UploadDate).ToList();
                
                // If a subId is specified, make sure it belongs to this parent
                var activeSub = doc.SubDocuments.FirstOrDefault(s => s.Id == subId);
                if (activeSub == null)
                {
                    activeSub = doc.SubDocuments.First();
                }
                ViewBag.ActiveSubDocument = activeSub;
            }

            doc.Comments = doc.Comments.OrderByDescending(c => c.CreatedDate).ToList();
            return View(doc);
        }

        // Download file
        public async Task<IActionResult> Download(int id)
        {
            // Chỉ cho phép người dùng đã đăng nhập tải tài liệu
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để tải tài liệu.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Details", "Document", new { id }) });
            }

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
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để thực hiện tải lên tài liệu.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Upload", "Document") });
            }

            ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.UploadType = "file";

            var userDisplayName = HttpContext.Session.GetString("UserDisplayName");
            var model = new Document
            {
                Author = userDisplayName ?? "Ẩn danh"
            };
            return View(model);
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

        private string? ExtractGoogleDriveFolderId(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();

            if (url.Contains("/folders/"))
            {
                try
                {
                    var startIndex = url.IndexOf("/folders/") + "/folders/".Length;
                    var endIndex = url.IndexOf("/", startIndex);
                    if (endIndex == -1) endIndex = url.IndexOf("?", startIndex);
                    if (endIndex == -1) endIndex = url.Length;
                    return url.Substring(startIndex, endIndex - startIndex);
                }
                catch { }
            }

            if (url.Contains("id=") && (url.Contains("folder") || url.Contains("open")))
            {
                try
                {
                    var match = System.Text.RegularExpressions.Regex.Match(url, @"[?&]id=([^&]+)");
                    if (match.Success)
                    {
                        return match.Groups[1].Value;
                    }
                }
                catch { }
            }

            return null;
        }

        private async Task<List<(string Id, string Title, string PreviewUrl)>> FetchGoogleDriveFolderFiles(string folderId, string? resourceKey)
        {
            var files = new List<(string Id, string Title, string PreviewUrl)>();
            var url = $"https://drive.google.com/embeddedfolderview?id={folderId}";
            if (!string.IsNullOrEmpty(resourceKey))
            {
                url += $"&resourcekey={resourceKey}";
            }

            using (var client = new System.Net.Http.HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                try
                {
                    var html = await client.GetStringAsync(url);
                    
                    var entryRegex = new System.Text.RegularExpressions.Regex(
                        @"class=""flip-entry""[^>]*id=""entry-(?<id>[a-zA-Z0-9_-]+)"".*?<a[^>]*href=""(?<href>[^""]+)""[^>]*>.*?class=""flip-entry-title""[^>]*>(?<title>[^<]+)</div>",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
                        
                    var matches = entryRegex.Matches(html);
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        var fileId = m.Groups["id"].Value;
                        var rawHref = m.Groups["href"].Value;
                        var rawTitle = m.Groups["title"].Value;

                        var href = System.Net.WebUtility.HtmlDecode(rawHref);
                        var cleanTitle = System.Net.WebUtility.HtmlDecode(rawTitle);

                        // Clean title from "Shared", "Video", etc. suffix
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\s+(Video|Shared|Document|PDF|Folder|Image|Audio|Archive)(\s+Shared)*$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\s+Shared$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                        // Extract resourcekey from file link
                        var fileResourceKey = "";
                        var fileResMatch = System.Text.RegularExpressions.Regex.Match(href, @"[?&]resourcekey=([^&]+)");
                        if (fileResMatch.Success)
                        {
                            fileResourceKey = fileResMatch.Groups[1].Value;
                        }

                        var previewUrl = $"https://drive.google.com/file/d/{fileId}/preview";
                        if (!string.IsNullOrEmpty(fileResourceKey))
                        {
                            previewUrl += $"?resourcekey={fileResourceKey}";
                        }

                        if (!files.Any(f => f.Id == fileId))
                        {
                            files.Add((fileId, cleanTitle, previewUrl));
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Lỗi khi cào thư mục Google Drive: {ex.Message}");
                }
            }
            
            return files;
        }

        private static int ExtractLessonNumber(string title)
        {
            var match = System.Text.RegularExpressions.Regex.Match(title, @"(?:Bài|Bai|Lesson|Lesson\s+)\s*(?<num>\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups["num"].Value, out var num))
            {
                return num;
            }
            return 999999;
        }

        // Upload form (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(Document model, IFormFile? file, string? uploadType, string? driveLink)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                TempData["ErrorMessage"] = "Vui lòng đăng nhập để thực hiện tải lên tài liệu.";
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action("Upload", "Document") });
            }

            // Remove server-generated fields from model validation
            ModelState.Remove(nameof(model.FilePath));
            ModelState.Remove(nameof(model.OriginalFileName));
            ModelState.Remove(nameof(model.FileExtension));
            ModelState.Remove(nameof(model.FileSize));

            bool isFolderImport = false;
            string? folderId = null;
            string? folderResourceKey = null;

            if (uploadType == "link" && !string.IsNullOrWhiteSpace(driveLink))
            {
                folderId = ExtractGoogleDriveFolderId(driveLink);
                if (folderId != null)
                {
                    isFolderImport = true;

                    // Extract resourcekey from the driveLink
                    var resKeyMatch = System.Text.RegularExpressions.Regex.Match(driveLink, @"[?&]resourcekey=([^&]+)");
                    if (resKeyMatch.Success)
                    {
                        folderResourceKey = resKeyMatch.Groups[1].Value;
                    }
                }
            }

            if (uploadType == "link")
            {
                if (string.IsNullOrWhiteSpace(driveLink))
                {
                    ModelState.AddModelError("driveLink", "Vui lòng nhập đường dẫn video hoặc thư mục Google Drive.");
                }
                else if (!isFolderImport)
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
                if (isFolderImport && folderId != null)
                {
                    // 1. Create Parent Document first
                    var parentDoc = new Document
                    {
                        Title = model.Title,
                        Description = model.Description ?? $"Thư mục tài liệu liên kết từ Google Drive: {model.Title}.",
                        CategoryId = model.CategoryId,
                        Level = model.Level,
                        Author = string.IsNullOrWhiteSpace(model.Author) ? "Ẩn danh" : model.Author.Trim(),
                        Language = model.Language,
                        Source = string.IsNullOrWhiteSpace(model.Source) ? "" : model.Source.Trim(),
                        Tags = string.IsNullOrWhiteSpace(model.Tags) ? "" : model.Tags.Trim(),
                        FilePath = driveLink,
                        OriginalFileName = "Google Drive Folder",
                        FileExtension = ".folder",
                        FileSize = 0,
                        UploadDate = DateTime.Now,
                        IsApproved = false
                    };
                    _context.Add(parentDoc);
                    await _context.SaveChangesAsync(); // Generates parentDoc.Id

                    // 2. Fetch files from folder using embeddedfolderview
                    var files = await FetchGoogleDriveFolderFiles(folderId, folderResourceKey);
                    if (files.Count == 0)
                    {
                        // Clean up parent doc if empty
                        _context.Remove(parentDoc);
                        await _context.SaveChangesAsync();

                        ModelState.AddModelError("driveLink", "Không tìm thấy tệp nào trong thư mục Google Drive này hoặc thư mục không công khai.");
                        ViewBag.Categories = await _context.Categories.OrderBy(c => c.Name).ToListAsync();
                        ViewBag.UploadType = uploadType;
                        ViewBag.DriveLink = driveLink;
                        return View(model);
                    }

                    // Sort naturally based on lesson number
                    files = files.OrderBy(f => ExtractLessonNumber(f.Title)).ThenBy(f => f.Title).ToList();

                    var now = DateTime.Now;
                    int seq = 0;
                    foreach (var fileInfo in files)
                    {
                        var childDoc = new Document
                        {
                            Title = fileInfo.Title,
                            Description = $"Mục thứ {seq + 1} trong thư mục Google Drive: {fileInfo.Title}.",
                            CategoryId = model.CategoryId,
                            Level = model.Level,
                            Author = parentDoc.Author,
                            Language = model.Language,
                            Source = parentDoc.Source,
                            Tags = parentDoc.Tags,
                            FilePath = fileInfo.PreviewUrl,
                            OriginalFileName = "Google Drive Video",
                            FileExtension = ".gdrive",
                            FileSize = 0,
                            UploadDate = now.AddSeconds(seq + 1),
                            IsApproved = false,
                            ParentId = parentDoc.Id // Link to parent
                        };
                        _context.Add(childDoc);
                        seq++;
                    }

                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = $"Đã tự động thêm {seq} bài học từ thư mục Google Drive vào hệ thống. Đang chờ Admin phê duyệt.";
                    return RedirectToAction("Index", "Home");
                }

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
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                return Json(new { success = false, message = "Vui lòng đăng nhập để đánh giá tài liệu." });
            }

            var userDisplayName = HttpContext.Session.GetString("UserDisplayName");
            var commentAuthor = !string.IsNullOrWhiteSpace(userDisplayName) ? userDisplayName : author;

            if (string.IsNullOrWhiteSpace(commentAuthor) || string.IsNullOrWhiteSpace(content))
                return Json(new { success = false, message = "Vui lòng nhập đầy đủ thông tin." });

            var doc = await _context.Documents.FindAsync(documentId);
            if (doc == null) return Json(new { success = false, message = "Không tìm thấy tài liệu." });

            var comment = new Comment
            {
                DocumentId  = documentId,
                Author      = commentAuthor.Trim(),
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
