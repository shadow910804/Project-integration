using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommercePlatform.Models
{
    [Table("ReviewReports")]
    public class ReviewReport
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ReviewId { get; set; }

        [Required]
        public int ReporterId { get; set; }

        [Required]
        [StringLength(100)]
        public string Reason { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsProcessed { get; set; } = false;
        public string? AdminResponse { get; set; }
        public DateTime? ProcessedAt { get; set; }

        // 檢舉類型 (對應 ProjectController 的檢舉功能)
        public bool Harassment { get; set; } = false;
        public bool Pornography { get; set; } = false;
        public bool Threaten { get; set; } = false;
        public bool Hatred { get; set; } = false;

        // 導航屬性
        public virtual Review Review { get; set; } = null!;
        public virtual User Reporter { get; set; } = null!;
    }
}
