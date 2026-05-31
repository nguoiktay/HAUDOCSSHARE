using System;
using System.Linq;
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

        public ChatbotController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Query([FromBody] ChatRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new ChatResponse
                {
                    Response = "Xin chào! Mình có thể giúp gì cho bạn hôm nay?",
                    Suggestions = new List<string> { "Tìm tài liệu Đại số", "Cách đăng tài liệu", "Quy chế duyệt bài" }
                });
            }

            string rawMsg = request.Message.Trim();
            string msg = rawMsg.ToLower();

            string response = "";
            var suggestions = new List<string>();

            // 1. GREETING INTENT
            if (msg.Contains("xin chào") || msg.Contains("hello") || msg.Contains("hi") || msg.Contains("chào") || msg.Contains("bắt đầu") || msg.Equals("bot"))
            {
                response = "<p>👋 <strong>Xin chào!</strong> Mình là Trợ lý ảo của <strong>HAUDOCSSHARE</strong> — Nền tảng chia sẻ học liệu dành riêng cho học viên Học viện Kỹ thuật Quân sự (MTA).</p>" +
                           "<p>Mình có thể giúp bạn:<br>" +
                           "🔍 <strong>Tìm tài liệu nhanh</strong> (ví dụ: gõ <em>'tìm tài liệu toán'</em>)<br>" +
                           "📤 Hướng dẫn <strong>đăng tải tài liệu</strong> mới<br>" +
                           "📥 Hướng dẫn <strong>tải tài liệu</strong> học tập<br>" +
                           "🛡️ Giải đáp <strong>quy trình kiểm duyệt</strong></p>" +
                           "<p>Bạn muốn bắt đầu với vấn đề nào?</p>";

                suggestions.AddRange(new[] { "Tìm tài liệu Công nghệ thông tin", "Cách đăng tài liệu?", "Quy chế kiểm duyệt?", "Liên hệ hỗ trợ" });
            }
            // 2. SEARCH INTENT (Contains "tìm", "kiếm", "search", "có tài liệu", "đề thi", "giáo trình")
            else if (msg.Contains("tìm") || msg.Contains("kiếm") || msg.Contains("search") || msg.Contains("có tài liệu") || msg.Contains("giáo trình") || msg.Contains("đề thi") || msg.Contains("bài tập"))
            {
                // Extract search keyword
                string keyword = rawMsg;
                string[] removePrefixes = new[] { "tìm tài liệu về", "tìm tài liệu", "tìm kiếm tài liệu", "tìm kiếm", "tìm giáo trình", "tìm đề thi", "tìm", "kiếm tài liệu về", "kiếm tài liệu", "kiếm", "search", "có tài liệu về", "có tài liệu", "không", "về", "môn" };
                
                string cleanedKeyword = msg;
                foreach (var prefix in removePrefixes)
                {
                    if (cleanedKeyword.StartsWith(prefix))
                    {
                        cleanedKeyword = cleanedKeyword.Substring(prefix.Length).Trim();
                        // Also clean up from raw message
                        int startIdx = rawMsg.ToLower().IndexOf(prefix);
                        if (startIdx >= 0)
                        {
                            keyword = rawMsg.Remove(startIdx, prefix.Length).Trim();
                        }
                        break;
                    }
                }

                // If cleaned keyword is too short, just do search on whole message or prompt user
                if (string.IsNullOrWhiteSpace(cleanedKeyword) || cleanedKeyword.Length < 2)
                {
                    response = "<p>🔍 Bạn muốn tìm tài liệu về môn học hoặc lĩnh vực nào? Hãy nhập cụ thể hơn nhé (ví dụ: <em>'Tìm tài liệu Giải tích 1'</em> hoặc <em>'C#'</em>).</p>";
                    suggestions.AddRange(new[] { "Tìm tài liệu C#", "Tìm tài liệu Cơ khí", "Tìm đề thi Vật lý" });
                }
                else
                {
                    var searchWord = cleanedKeyword.ToLower();
                    // Query DB for approved documents matching the keyword
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
                        response = $"<p>🔍 Tìm thấy <strong>{docs.Count}</strong> tài liệu phù hợp với từ khóa <strong>\"{keyword}\"</strong> của bạn:</p>" +
                                   "<div class='chat-doc-list'>";

                        foreach (var d in docs)
                        {
                            string sizeStr = d.FileSize > 0 ? $" ({(d.FileSize / 1024.0 / 1024.0):0.#} MB)" : "";
                            string extBadge = d.FileExtension.TrimStart('.').ToUpper();
                            response += $"<div class='chat-doc-item'>" +
                                        $"  <a href='/Document/Details/{d.Id}' target='_blank' class='chat-doc-link'>" +
                                        $"    <i class='bi bi-file-earmark-text text-info'></i> " +
                                        $"    <strong>{d.Title}</strong>" +
                                        $"  </a>" +
                                        $"  <div class='chat-doc-meta'>" +
                                        $"    <span class='badge bg-secondary' style='font-size:.65rem;'>{extBadge}</span>" +
                                        $"    <span style='font-size:.7rem; color:var(--text-muted); margin-left:.5rem;'>Tác giả: {d.Author}</span>" +
                                        $"  </div>" +
                                        $"</div>";
                        }

                        response += "</div>" +
                                    "<p class='mt-2 small text-muted'><i class='bi bi-info-circle'></i> Click vào tên tài liệu để xem chi tiết và tải xuống nhé!</p>";
                        
                        suggestions.AddRange(new[] { "Tìm tài liệu khác", "Đăng tài liệu mới", "Quay lại trang chủ" });
                    }
                    else
                    {
                        response = $"<p>❌ Rất tiếc, mình không tìm thấy tài liệu nào khớp với từ khóa <strong>\"{keyword}\"</strong>.</p>" +
                                   $"<p>💡 <strong>Lời khuyên:</strong><br>" +
                                   $"- Hãy kiểm tra lại chính tả hoặc rút ngắn từ khóa chính.<br>" +
                                   $"- Thử tìm theo mã môn học hoặc tên giáo viên.<br>" +
                                   $"- Bạn cũng có thể vào <a href='/Document/Browse' class='text-accent'>Trang danh sách tài liệu</a> để tìm thủ công.</p>";
                        
                        suggestions.AddRange(new[] { "Tìm tài liệu C++", "Tìm đề thi Đại số", "Cách đăng tài liệu?" });
                    }
                }
            }
            // 3. UPLOAD INTENT
            else if (msg.Contains("đăng") || msg.Contains("upload") || msg.Contains("tải lên") || msg.Contains("chia sẻ"))
            {
                response = "<p>📤 <strong>Hướng dẫn đăng tài liệu học tập:</strong></p>" +
                           "<ol>" +
                           "  <li>Truy cập mục <strong><a href='/Document/Upload'>Đăng tải</a></strong> trên thanh điều hướng.</li>" +
                           "  <li>Chọn phương thức: <strong>Tải tệp từ máy</strong> (hỗ trợ PDF, DOCX, ZIP... tối đa 50MB) hoặc <strong>Link Google Drive</strong> (dành cho video bài giảng hoặc thư mục lớn).</li>" +
                           "  <li>Nhập đầy đủ thông tin: Tiêu đề, danh mục ngành học, cấp độ học, tác giả và mô tả ngắn.</li>" +
                           "  <li>Nhấn <strong>Gửi để duyệt</strong>. Bài đăng sẽ hiển thị sau khi Admin phê duyệt.</li>" +
                           "</ol>" +
                           "<p>💡 <em>Mẹo:</em> Link Google Drive nên được chia sẻ ở chế độ 'Bất kỳ ai có liên kết đều có thể xem' để hệ thống tự động nhúng khung preview nhé!</p>";

                suggestions.AddRange(new[] { "Bao lâu bài được duyệt?", "Quy chế kiểm duyệt?", "Tìm tài liệu" });
            }
            // 4. DOWNLOAD INTENT
            else if (msg.Contains("tải") || msg.Contains("download") || msg.Contains("tải về") || msg.Contains("lưu"))
            {
                response = "<p>📥 <strong>Hướng dẫn tải tài liệu:</strong></p>" +
                           "<p>Để tải xuống bất kỳ tài liệu nào trên HAUDOCSSHARE:</p>" +
                           "<ul>" +
                           "  <li>Trên thẻ tài liệu tại trang chủ/danh sách: Nhấn nút <strong>Tải xuống</strong> màu xanh cyan.</li>" +
                           "  <li>Trong trang chi tiết tài liệu: Nhấn nút <strong>Tải xuống trực tiếp</strong>.</li>" +
                           "  <li>Đối với các video hoặc tài liệu dạng Google Drive: Trình duyệt sẽ mở liên kết tải trực tiếp hoặc hiển thị trình phát video ngay trên web của chúng ta.</li>" +
                           "</ul>" +
                           "<p>🔓 Tất cả tài liệu đều được chia sẻ <strong>hoàn toàn miễn phí</strong> nhằm phục vụ mục đích học tập của học viên MTA!</p>";

                suggestions.AddRange(new[] { "Tìm đề thi Giải tích 2", "Cách đăng tài liệu?", "Liên hệ Admin" });
            }
            // 5. MODERATION / QUEUE INTENT
            else if (msg.Contains("duyệt") || msg.Contains("kiểm duyệt") || msg.Contains("phê duyệt") || msg.Contains("chờ") || msg.Contains("bao lâu"))
            {
                response = "<p>🛡️ <strong>Quy trình kiểm duyệt bài đăng:</strong></p>" +
                           "<p>Để bảo đảm an toàn dữ liệu và chất lượng tài liệu học tập, tránh spam và tài liệu độc hại:</p>" +
                           "<ul>" +
                           "  <li>Tất cả tài liệu sau khi đăng tải sẽ nằm ở trạng thái <strong>Chờ duyệt (Pending)</strong>.</li>" +
                           "  <li>Ban quản trị (Admin) sẽ xem xét nội dung, định dạng file, tính chính xác và an toàn.</li>" +
                           "  <li>Quá trình kiểm duyệt thường diễn ra từ <strong>1 đến 4 giờ</strong>.</li>" +
                           "  <li>Sau khi được duyệt, tài liệu sẽ hiển thị công khai tới toàn bộ cộng đồng HAUDOCSSHARE.</li>" +
                           "</ul>";

                suggestions.AddRange(new[] { "Cách đăng tài liệu?", "Liên hệ Admin hỗ trợ", "Quay lại trang chủ" });
            }
            // 6. CATEGORIES INTENT
            else if (msg.Contains("danh mục") || msg.Contains("ngành") || msg.Contains("lĩnh vực") || msg.Contains("môn học") || msg.Contains("khoa"))
            {
                response = "<p>📚 <strong>Các lĩnh vực tài liệu hiện có trên HAUDOCSSHARE:</strong></p>" +
                           "<ul>" +
                           "  <li>💻 <strong>Công nghệ thông tin:</strong> Lập trình, mạng máy tính, AI, DevOps...</li>" +
                           "  <li>📈 <strong>Kinh tế & Kinh doanh:</strong> Tài chính, quản trị, marketing...</li>" +
                           "  <li>⚙️ <strong>Khoa học & Kỹ thuật:</strong> Cơ khí, điện tử, xây dựng...</li>" +
                           "  <li>🩺 <strong>Y tế & Sức khỏe:</strong> Y học, dược, dinh dưỡng...</li>" +
                           "  <li>⚖️ <strong>Luật & Pháp lý:</strong> Luật dân sự, hiến pháp...</li>" +
                           "  <li>🗣️ <strong>Ngoại ngữ:</strong> Tiếng Anh, IELTS, TOEIC, tiếng Nhật...</li>" +
                           "  <li>🎨 <strong>Nghệ thuật & Thiết kế:</strong> Đồ họa, UX/UI, âm nhạc...</li>" +
                           "  <li>🤝 <strong>Kỹ năng mềm:</strong> Giao tiếp, lãnh đạo, quản lý thời gian...</li>" +
                           "</ul>";

                suggestions.AddRange(new[] { "Tìm tài liệu Công nghệ thông tin", "Tìm tài liệu Ngoại ngữ", "Cách đăng tài liệu?" });
            }
            // 7. MTA / CREATOR INTENT
            else if (msg.Contains("mta") || msg.Contains("học viện") || msg.Contains("quân sự") || msg.Contains("đề tài") || msg.Contains("tác giả") || msg.Contains("ai làm"))
            {
                response = "<p>🎓 <strong>Giới thiệu dự án HAUDOCSSHARE:</strong></p>" +
                           "<p>Dự án này được xây dựng bởi nhóm sinh viên MTA làm đề tài môn học <strong>Công nghệ Web</strong>.</p>" +
                           "<p>💻 <strong>Công nghệ sử dụng:</strong> ASP.NET Core 10, Entity Framework Core, SQLite, kết hợp thiết kế UI/UX hiện đại theo trường phái Claymorphism và Glassmorphism.</p>" +
                           "<p>Chúng mình mong muốn tạo lập một không gian lưu trữ tài liệu tập trung, giúp các bạn học viên khóa dưới dễ dàng tiếp cận giáo trình ôn thi và bài tập lớn từ các khóa trước.</p>";

                suggestions.AddRange(new[] { "Tìm đề thi MTA", "Cách đăng tài liệu?", "Liên hệ Admin" });
            }
            // 8. CONTACT / HELP
            else if (msg.Contains("liên hệ") || msg.Contains("hỗ trợ") || msg.Contains("admin") || msg.Contains("giúp"))
            {
                response = "<p>✉️ <strong>Thông tin liên hệ & Hỗ trợ:</strong></p>" +
                           "<p>Nếu bạn gặp sự cố kỹ thuật, lỗi đăng nhập, hoặc muốn báo cáo tài liệu vi phạm bản quyền:</p>" +
                           "<ul>" +
                           "  <li>📧 Email ban quản trị: <a href='mailto:admin@haudocsshare.local' class='text-accent'>admin@haudocsshare.local</a></li>" +
                           "  <li>🏫 Văn phòng: Tòa nhà Khoa CNTT, Học viện Kỹ thuật Quân sự.</li>" +
                           "  <li>💬 Chat trực tiếp: Gửi câu hỏi tại đây để mình giải đáp nhé!</li>" +
                           "</ul>";

                suggestions.AddRange(new[] { "Quay lại trang chủ", "Tìm tài liệu C#", "Cách đăng tài liệu" });
            }
            // 9. DEFAULT / BOT EXPLANATION
            else
            {
                response = "<p>🤖 <strong>Xin lỗi, mình chưa hiểu rõ câu hỏi của bạn.</strong></p>" +
                           "<p>Mình là Trợ lý ảo hỗ trợ học tập của HAUDOCSSHARE. Bạn có thể hỏi mình các thông tin liên quan đến:<br>" +
                           "- Tìm kiếm tài liệu: gõ <em>'tìm tài liệu [tên tài liệu]'</em><br>" +
                           "- Quy trình đăng tài liệu hoặc tải xuống<br>" +
                           "- Quy chế kiểm duyệt bài viết<br>" +
                           "- Thông tin liên hệ Admin</p>" +
                           "<p>Hoặc bạn có thể click chọn các gợi ý bên dưới nhé!</p>";

                suggestions.AddRange(new[] { "Tìm tài liệu học tập", "Cách đăng tài liệu", "Liên hệ Admin hỗ trợ" });
            }

            return Json(new ChatResponse
            {
                Response = response,
                Suggestions = suggestions
            });
        }
    }

    public class ChatRequest
    {
        public string Message { get; set; } = string.Empty;
    }

    public class ChatResponse
    {
        public string Response { get; set; } = string.Empty;
        public List<string> Suggestions { get; set; } = new List<string>();
    }
}
