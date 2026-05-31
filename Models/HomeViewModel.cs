using System.Collections.Generic;

namespace Documentshare.Models
{
    public class HomeViewModel
    {
        public List<Document> Documents { get; set; } = new();
        public List<Category> Categories { get; set; } = new();

        // Thống kê
        public int TotalDocuments { get; set; }
        public int TotalDownloads { get; set; }
        public int TotalViews { get; set; }
        public int TotalCategories { get; set; }

        // Bộ lọc tìm kiếm
        public string SearchString { get; set; } = string.Empty;
        public int? SelectedCategoryId { get; set; }
        public string SelectedLevel { get; set; } = string.Empty;
        public string SelectedExtension { get; set; } = string.Empty;
        public string SelectedLanguage { get; set; } = string.Empty;
        public string SortBy { get; set; } = "newest";

        // Tài liệu nổi bật (nhiều tải nhất)
        public List<Document> FeaturedDocuments { get; set; } = new();
    }
}
