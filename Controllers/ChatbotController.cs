using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Documentshare.Data;
using Documentshare.Models;
using System.Collections.Generic;

namespace Documentshare.Controllers
{
    public class ChatbotController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;

        // URL của Python AI Service
        private const string PythonServiceUrl = "http://localhost:8001";

        public ChatbotController(AppDbContext context, IHttpClientFactory httpClientFactory)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
        }

        [HttpPost]
        public async Task<IActionResult> Query([FromBody] ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new ChatResponse
                {
                    Response = "<p>👋 <strong>Xin chào!</strong> Mình là Trợ lý ảo HAUDOCSSHARE — được hỗ trợ bởi AI Gemini. Hãy hỏi mình bất kỳ điều gì!</p>",
                    Suggestions = new List<string> { "Tìm tài liệu Đại số", "Cách đăng tài liệu", "Quy chế duyệt bài" }
                });
            }

            // ── Thử gọi Python AI Service ────────────────────────────────────
            try
            {
                var aiResponse = await CallPythonAIService(request.Message, request.History);
                if (aiResponse != null)
                {
                    return Json(aiResponse);
                }
            }
            catch (Exception ex)
            {
                // Log nhưng không throw — fallback về keyword matching
                Console.WriteLine($"[ChatbotController] Python AI Service không khả dụng: {ex.Message}");
            }

            // ── FALLBACK: Keyword matching (giữ lại khi Python service tắt) ──
            return Json(await KeywordFallback(request.Message));
        }

        // ─────────────────────────────────────────────────────────────────────
        // Gọi Python FastAPI AI Service
        // ─────────────────────────────────────────────────────────────────────
        private async Task<ChatResponse?> CallPythonAIService(string message, List<ChatHistoryItem>? history)
        {
            var client = _httpClientFactory.CreateClient("PythonChatbot");

            var payload = new
            {
                message = message,
                history = history?.Select(h => new { role = h.Role, content = h.Content }).ToList()
            };

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(45));
            var httpResponse = await client.PostAsync($"{PythonServiceUrl}/query", jsonContent, cts.Token);

            if (!httpResponse.IsSuccessStatusCode)
            {
                Console.WriteLine($"[ChatbotController] Python service lỗi HTTP {httpResponse.StatusCode}");
                return null;
            }

            var result = await httpResponse.Content.ReadFromJsonAsync<PythonChatResponse>();
            if (result == null) return null;

            return new ChatResponse
            {
                Response   = result.Response ?? "",
                Suggestions = result.Suggestions ?? new List<string>()
            };
        }

        // ─────────────────────────────────────────────────────────────────────
        // FALLBACK: Keyword matching (giữ nguyên logic cũ để dự phòng)
        // ─────────────────────────────────────────────────────────────────────
        private async Task<ChatResponse> KeywordFallback(string rawMsg)
        {
            string msg = rawMsg.ToLower().Trim();
            string response;
            var suggestions = new List<string>();

            // 1. GREETING
            if (msg.Contains("xin chào") || msg.Contains("hello") || msg.Contains("hi") ||
                msg.Contains("chào") || msg.Contains("bắt đầu") || msg.Equals("bot"))
            {
                response = "<p>👋 <strong>Xin chào!</strong> Mình là Trợ lý ảo HAUDOCSSHARE.</p>" +
                           "<p>⚠️ <em>Lưu ý: AI Gemini đang offline. Mình đang chạy ở chế độ cơ bản.</em></p>" +
                           "<p>Mình có thể giúp: 🔍 Tìm tài liệu · 📤 Hướng dẫn đăng tải · 🛡️ Quy chế duyệt bài</p>";
                suggestions.AddRange(new[] { "Tìm tài liệu C#", "Cách đăng tài liệu?", "Quy chế kiểm duyệt?" });
            }
            // 2. SEARCH
            else if (msg.Contains("tìm") || msg.Contains("kiếm") || msg.Contains("search") ||
                     msg.Contains("có tài liệu") || msg.Contains("giáo trình") ||
                     msg.Contains("đề thi") || msg.Contains("bài tập"))
            {
                string keyword = rawMsg;
                string[] removePrefixes = { "tìm tài liệu về", "tìm tài liệu", "tìm kiếm tài liệu", "tìm kiếm", "tìm giáo trình", "tìm đề thi", "tìm", "kiếm tài liệu về", "kiếm tài liệu", "kiếm", "search", "có tài liệu về", "có tài liệu", "không", "về", "môn" };
                string cleanedKeyword = msg;
                foreach (var prefix in removePrefixes)
                {
                    if (cleanedKeyword.StartsWith(prefix))
                    {
                        cleanedKeyword = cleanedKeyword.Substring(prefix.Length).Trim();
                        int startIdx = rawMsg.ToLower().IndexOf(prefix);
                        if (startIdx >= 0) keyword = rawMsg.Remove(startIdx, prefix.Length).Trim();
                        break;
                    }
                }

                if (string.IsNullOrWhiteSpace(cleanedKeyword) || cleanedKeyword.Length < 2)
                {
                    response = "<p>🔍 Bạn muốn tìm tài liệu về môn học nào? Hãy nhập cụ thể hơn nhé (ví dụ: <em>'C#'</em>).</p>";
                    suggestions.AddRange(new[] { "Tìm tài liệu C#", "Tìm tài liệu Cơ khí", "Tìm đề thi Vật lý" });
                }
                else
                {
                    var searchWord = cleanedKeyword.ToLower();
                    var docs = await _context.Documents
                        .Include(d => d.Category)
                        .Where(d => d.IsApproved &&
                                   (d.Title.ToLower().Contains(searchWord) ||
                                    d.Description.ToLower().Contains(searchWord) ||
                                    d.Tags.ToLower().Contains(searchWord) ||
                                    d.Author.ToLower().Contains(searchWord)))
                        .Take(5)
                        .ToListAsync();

                    if (docs.Any())
                    {
                        response = $"<p>🔍 Tìm thấy <strong>{docs.Count}</strong> tài liệu khớp với <strong>\"{keyword}\"</strong>:</p><div class='chat-doc-list'>";
                        foreach (var d in docs)
                        {
                            string ext = d.FileExtension.TrimStart('.').ToUpper();
                            response += $"<div class='chat-doc-item'><a href='/Document/Details/{d.Id}' target='_blank' class='chat-doc-link'><i class='bi bi-file-earmark-text text-info'></i> <strong>{d.Title}</strong></a><div class='chat-doc-meta'><span class='badge bg-secondary' style='font-size:.65rem;'>{ext}</span><span style='font-size:.7rem;color:var(--text-muted);margin-left:.5rem;'>Tác giả: {d.Author}</span></div></div>";
                        }
                        response += "</div><p class='mt-2 small text-muted'><i class='bi bi-info-circle'></i> Click vào tên tài liệu để xem chi tiết!</p>";
                        suggestions.AddRange(new[] { "Tìm tài liệu khác", "Đăng tài liệu mới", "Quay lại trang chủ" });
                    }
                    else
                    {
                        response = $"<p>❌ Không tìm thấy tài liệu khớp với <strong>\"{keyword}\"</strong>.</p><p>💡 Thử tìm theo mã môn học hoặc vào <a href='/Document/Browse' class='text-accent'>trang danh sách</a> nhé.</p>";
                        suggestions.AddRange(new[] { "Tìm tài liệu C++", "Tìm đề thi Đại số", "Cách đăng tài liệu?" });
                    }
                }
            }
            // 3. UPLOAD
            else if (msg.Contains("đăng") || msg.Contains("upload") || msg.Contains("tải lên") || msg.Contains("chia sẻ"))
            {
                response = "<p>📤 <strong>Hướng dẫn đăng tài liệu:</strong></p><ol><li>Truy cập <a href='/Document/Upload'><strong>Đăng tải</strong></a> trên thanh điều hướng.</li><li>Chọn: Tải file (PDF, DOCX, ZIP... tối đa 50MB) hoặc Link Google Drive.</li><li>Nhập tiêu đề, danh mục, cấp độ, tác giả, mô tả.</li><li>Nhấn <strong>Gửi để duyệt</strong>. Bài đăng sẽ hiển thị sau khi Admin phê duyệt.</li></ol>";
                suggestions.AddRange(new[] { "Bao lâu bài được duyệt?", "Quy chế kiểm duyệt?", "Tìm tài liệu" });
            }
            // 4. DOWNLOAD
            else if (msg.Contains("tải") || msg.Contains("download") || msg.Contains("lưu"))
            {
                response = "<p>📥 <strong>Hướng dẫn tải tài liệu:</strong></p><ul><li>Tại trang chủ/danh sách: Nhấn nút <strong>Tải xuống</strong>.</li><li>Trong trang chi tiết: Nhấn <strong>Tải xuống trực tiếp</strong>.</li></ul><p>🔓 Tất cả tài liệu đều <strong>miễn phí</strong>!</p>";
                suggestions.AddRange(new[] { "Tìm đề thi Giải tích 2", "Cách đăng tài liệu?", "Liên hệ Admin" });
            }
            // 5. MODERATION
            else if (msg.Contains("duyệt") || msg.Contains("kiểm duyệt") || msg.Contains("chờ") || msg.Contains("bao lâu"))
            {
                response = "<p>🛡️ <strong>Quy trình kiểm duyệt:</strong></p><ul><li>Tài liệu sau khi đăng sẽ ở trạng thái <strong>Chờ duyệt</strong>.</li><li>Admin xem xét nội dung và tính an toàn.</li><li>Quá trình kiểm duyệt từ <strong>1 đến 4 giờ</strong>.</li><li>Sau khi duyệt → hiển thị công khai.</li></ul>";
                suggestions.AddRange(new[] { "Cách đăng tài liệu?", "Liên hệ Admin hỗ trợ", "Quay lại trang chủ" });
            }
            // 6. DEFAULT
            else
            {
                response = "<p>🤖 Mình chưa hiểu rõ câu hỏi. <em>(AI Gemini đang offline — đang chạy chế độ cơ bản)</em></p><p>Bạn có thể hỏi về: tìm tài liệu, cách đăng tải, quy chế duyệt bài hoặc liên hệ Admin.</p>";
                suggestions.AddRange(new[] { "Tìm tài liệu học tập", "Cách đăng tài liệu", "Liên hệ Admin hỗ trợ" });
            }

            return new ChatResponse { Response = response, Suggestions = suggestions };
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────
    public class ChatHistoryItem
    {
        public string Role    { get; set; } = "user";
        public string Content { get; set; } = string.Empty;
    }

    public class ChatRequest
    {
        public string Message                 { get; set; } = string.Empty;
        public List<ChatHistoryItem>? History { get; set; }
    }

    public class ChatResponse
    {
        public string       Response    { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new();
    }

    // ── Internal DTO từ Python service ───────────────────────────────────────
    internal class PythonChatResponse
    {
        public string?       Response    { get; set; }
        public List<string>? Suggestions { get; set; }
        public int           DocsFound   { get; set; }
    }
}
