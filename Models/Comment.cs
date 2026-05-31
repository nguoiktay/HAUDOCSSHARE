using System;
using System.ComponentModel.DataAnnotations;

namespace Documentshare.Models
{
    public class Comment
    {
        public int Id { get; set; }

        [Required]
        public int DocumentId { get; set; }
        public Document? Document { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên của bạn.")]
        [StringLength(100, ErrorMessage = "Tên không quá 100 ký tự.")]
        public string Author { get; set; } = "Người dùng";

        [Required(ErrorMessage = "Nội dung bình luận không được để trống.")]
        [StringLength(500, ErrorMessage = "Bình luận không quá 500 ký tự.")]
        public string Content { get; set; } = string.Empty;

        [Range(1, 5, ErrorMessage = "Đánh giá từ 1 đến 5 sao.")]
        public int Rating { get; set; } = 5;

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
