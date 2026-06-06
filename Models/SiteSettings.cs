using System.ComponentModel.DataAnnotations;

namespace Documentshare.Models
{
    // Bảng lưu thông tin cấu hình footer của trang web, Admin có thể chỉnh sửa qua Admin Panel
    public class SiteSettings
    {
        public int Id { get; set; }

        [MaxLength(200)]
        public string SiteName { get; set; } = "HAUDOCSSHARE";

        [MaxLength(500)]
        public string SiteDescription { get; set; } = "Nền tảng chia sẻ tài liệu học tập, nghiên cứu & chuyên môn cho mọi lĩnh vực.";

        [MaxLength(200)]
        public string FooterEmail { get; set; } = "admin@haudocsshare.local";

        [MaxLength(300)]
        public string FooterAddress { get; set; } = "Văn phòng tại Trường Đại học Kiến trúc Hà Nội";

        [MaxLength(200)]
        public string CopyrightText { get; set; } = "© 2026 HAUDOCSSHARE";

        [MaxLength(500)]
        public string FooterTagline { get; set; } = "Xây dựng bằng ASP.NET Core 10 & SQLite";
    }
}
