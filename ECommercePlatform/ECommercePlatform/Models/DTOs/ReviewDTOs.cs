using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models.DTOs
{
    public class ReviewDTOs
    {
        // 評價請求模型
        public class CreateReviewRequest
        {
            [Required]
            public int ProductId { get; set; }

            [Required]
            [StringLength(1000, MinimumLength = 10, ErrorMessage = "評價內容必須在10-1000字之間")]
            public string Content { get; set; } = string.Empty;

            [Required]
            [Range(1, 5, ErrorMessage = "評分必須在1-5星之間")]
            public int Rating { get; set; }

            public IFormFile? ImageFile { get; set; }
        }

        // 檢舉請求模型
        public class ReportReviewRequest
        {
            [Required]
            public int ReviewId { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "檢舉原因不能超過100字")]
            public string Reason { get; set; } = string.Empty;

            [StringLength(500, ErrorMessage = "詳細描述不能超過500字")]
            public string? Description { get; set; }

            public bool Harassment { get; set; } = false;
            public bool Pornography { get; set; } = false;
            public bool Threaten { get; set; } = false;
            public bool Hatred { get; set; } = false;
        }
    }
}
