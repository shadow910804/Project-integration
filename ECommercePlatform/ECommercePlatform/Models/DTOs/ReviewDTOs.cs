using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models.DTOs
{
    public class ReviewDTOs
    {
        // 評價請求模型
        public class CreateReviewRequest
        {
            public int ProductId { get; set; }
            public string Content { get; set; } = string.Empty;
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
            public string? Description { get; set; }

            public bool Harassment { get; set; } = false;
            public bool Pornography { get; set; } = false;
            public bool Threaten { get; set; } = false;
            public bool Hatred { get; set; } = false;
        }

        public class UpdateReviewRequest
        {
            public int ReviewId { get; set; }
            public string Content { get; set; } = string.Empty;
            public int Rating { get; set; }
            public IFormFile? ImageFile { get; set; }
        }
    }
}
