using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ECommercePlatform.Models
{
    [Table("Reviews")]
    public class Review
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int ProductId { get; set; }

        [StringLength(100)]
        public string? UserName { get; set; }

        [Required]
        [StringLength(1000, ErrorMessage = "評價內容不能超過1000字")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Range(1, 5, ErrorMessage = "評分必須在1-5星之間")]
        public int Rating { get; set; }

        // 評價圖片（儲存為二進制數據）
        public byte[]? ImageData { get; set; }

        // 圖片檔案名稱（用於識別）
        [StringLength(255)]
        public string? ImageFileName { get; set; }

        // 圖片MIME類型
        [StringLength(50)]
        public string? ImageContentType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [Required]
        public bool IsVisible { get; set; } = true;

        // 是否置頂（優質評價）
        public bool IsPinned { get; set; } = false;

        // 管理員回覆
        [StringLength(500)]
        public string? AdminReply { get; set; }
        public DateTime? AdminReplyTime { get; set; }
        public string? AdminRepliedBy { get; set; }

        // 回覆相關（用於評價回覆功能）
        public int? ReplyId { get; set; }

        // 評價有用性投票
        public int HelpfulCount { get; set; } = 0;
        public int UnhelpfulCount { get; set; } = 0;

        // 導航屬性
        public virtual User User { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
        public virtual Review? ReplyTo { get; set; }
        public virtual ICollection<Review> Replies { get; set; } = new List<Review>();
        public virtual ICollection<ReviewReport> Reports { get; set; } = new List<ReviewReport>();

        // 計算屬性
        [NotMapped]
        public bool IsReply => ReplyId.HasValue;

        [NotMapped]
        public string DisplayUserName => UserName ?? User?.Username ?? "匿名用戶";

        [NotMapped]
        public string RatingStars
        {
            get
            {
                var stars = "";
                for (int i = 1; i <= 5; i++)
                {
                    stars += i <= Rating ? "★" : "☆";
                }
                return stars;
            }
        }

        [NotMapped]
        public bool HasImage => ImageData != null && ImageData.Length > 0;

        [NotMapped]
        public string FormattedDate => CreatedAt.ToString("yyyy-MM-dd HH:mm");

        [NotMapped]
        public string RelativeTime
        {
            get
            {
                var timeSpan = DateTime.UtcNow - CreatedAt;
                if (timeSpan.TotalDays >= 1)
                    return $"{(int)timeSpan.TotalDays} 天前";
                if (timeSpan.TotalHours >= 1)
                    return $"{(int)timeSpan.TotalHours} 小時前";
                if (timeSpan.TotalMinutes >= 1)
                    return $"{(int)timeSpan.TotalMinutes} 分鐘前";
                return "剛剛";
            }
        }

        [NotMapped]
        public bool HasAdminReply => !string.IsNullOrEmpty(AdminReply);

        [NotMapped]
        public double HelpfulPercentage
        {
            get
            {
                var total = HelpfulCount + UnhelpfulCount;
                return total > 0 ? (double)HelpfulCount / total * 100 : 0;
            }
        }

        [NotMapped]
        public string RatingColor => Rating switch
        {
            5 => "text-success",
            4 => "text-info",
            3 => "text-warning",
            2 => "text-orange",
            1 => "text-danger",
            _ => "text-muted"
        };

        [NotMapped]
        public string RatingText => Rating switch
        {
            5 => "非常滿意",
            4 => "滿意",
            3 => "普通",
            2 => "不滿意",
            1 => "非常不滿意",
            _ => "未評分"
        };
    }
}