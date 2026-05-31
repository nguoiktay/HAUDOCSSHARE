using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Documentshare.Models
{
    public class Document
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề tài liệu.")]
        [StringLength(200, ErrorMessage = "Tiêu đề không quá 200 ký tự.")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

    
        public string FilePath { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public long FileSize { get; set; }

        [StringLength(20)]
        public string FileExtension { get; set; } = string.Empty;

        public DateTime UploadDate { get; set; } = DateTime.Now;
        public int DownloadCount { get; set; } = 0;
        public int ViewCount { get; set; } = 0;

        [Required(ErrorMessage = "Vui lòng chọn danh mục.")]
        public int CategoryId { get; set; }
        public Category? Category { get; set; }

        [StringLength(50)]
        public string Level { get; set; } = "Tất cả cấp độ";

        [StringLength(100)]
        public string Author { get; set; } = "Ẩn danh";

        [StringLength(500)]
        public string Tags { get; set; } = string.Empty;

        [StringLength(200)]
        public string Source { get; set; } = string.Empty;
        
        [StringLength(50)]
        public string Language { get; set; } = "Tiếng Việt";

        public bool IsApproved { get; set; } = false;

        public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    }
}
