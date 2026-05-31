using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Documentshare.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string Description { get; set; } = string.Empty;

        [StringLength(100)]
        public string Icon { get; set; } = "bi-folder";

        [StringLength(50)]
        public string ColorClass { get; set; } = "gradient-blue";

        public ICollection<Document> Documents { get; set; } = new List<Document>();
    }
}
