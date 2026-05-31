using System;
using Microsoft.EntityFrameworkCore;
using Documentshare.Models;

namespace Documentshare.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Category> Categories { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Document>()
                .HasOne(d => d.Category)
                .WithMany(c => c.Documents)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Comment>()
                .HasOne(c => c.Document)
                .WithMany(d => d.Comments)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1,  Name = "Công nghệ thông tin",   Description = "Lập trình, phần mềm, mạng máy tính, AI, DevOps",          Icon = "bi-code-slash",        ColorClass = "gradient-blue"    },
                new Category { Id = 2,  Name = "Kinh tế & Kinh doanh",  Description = "Quản trị, tài chính, marketing, kế toán, khởi nghiệp",     Icon = "bi-graph-up-arrow",    ColorClass = "gradient-green"   },
                new Category { Id = 3,  Name = "Khoa học & Kỹ thuật",   Description = "Vật lý, hóa học, sinh học, cơ khí, điện tử, xây dựng",    Icon = "bi-lightbulb",         ColorClass = "gradient-yellow"  },
                new Category { Id = 4,  Name = "Y tế & Sức khỏe",       Description = "Y học, dược, điều dưỡng, dinh dưỡng, sức khỏe tâm thần",  Icon = "bi-heart-pulse",       ColorClass = "gradient-red"     },
                new Category { Id = 5,  Name = "Luật & Pháp lý",        Description = "Luật dân sự, hình sự, thương mại, lao động, hiến pháp",    Icon = "bi-shield-check",      ColorClass = "gradient-purple"  },
                new Category { Id = 6,  Name = "Ngoại ngữ",             Description = "Tiếng Anh, Nhật, Hàn, Trung, Pháp, IELTS, TOEIC",         Icon = "bi-translate",         ColorClass = "gradient-cyan"    },
                new Category { Id = 7,  Name = "Khoa học xã hội",       Description = "Lịch sử, địa lý, xã hội học, tâm lý học, triết học",       Icon = "bi-people",            ColorClass = "gradient-orange"  },
                new Category { Id = 8,  Name = "Nghệ thuật & Thiết kế", Description = "Đồ họa, nhiếp ảnh, âm nhạc, kiến trúc, UX/UI",           Icon = "bi-palette",           ColorClass = "gradient-pink"    },
                new Category { Id = 9,  Name = "Giáo dục phổ thông",    Description = "Tài liệu THPT, THCS, đề thi, ôn tập các môn học",         Icon = "bi-mortarboard",       ColorClass = "gradient-teal"    },
                new Category { Id = 10, Name = "Kỹ năng mềm",           Description = "Giao tiếp, lãnh đạo, quản lý thời gian, tư duy phản biện", Icon = "bi-person-check",      ColorClass = "gradient-emerald" },
                new Category { Id = 11, Name = "Nông nghiệp & Môi trường",Description="Nông lâm ngư nghiệp, bảo vệ môi trường, biến đổi khí hậu", Icon = "bi-tree",              ColorClass = "gradient-lime"    },
                new Category { Id = 12, Name = "Khác",                   Description = "Các tài liệu không thuộc danh mục cụ thể nào",            Icon = "bi-folder2-open",      ColorClass = "gradient-slate"   }
            );

            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id          = 1,
                    Username    = "admin",
                    DisplayName = "Quản trị viên",
                    Email       = "admin@docshare.local",
                    PasswordHash = "$2a$11$RRwjFuVLhRbHX9bxTKQBpOIm6oHiOhYI3S1pO4fNzI7zGQRJpJEGe",
                    Role        = "Admin",
                    CreatedAt   = new DateTime(2026, 1, 1),
                    IsActive    = true
                }
            );
        }
    }
}
