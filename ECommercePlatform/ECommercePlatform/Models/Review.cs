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
        [StringLength(1000, ErrorMessage = "�������e����W�L1000�r")]
        public string Content { get; set; } = string.Empty;

        [Required]
        [Range(1, 5, ErrorMessage = "���������b1-5�P����")]
        public int Rating { get; set; }

        // �����Ϥ��]�x�s���G�i��ƾڡ^
        public byte[]? ImageData { get; set; }

        // �Ϥ��ɮצW�١]�Ω��ѧO�^
        [StringLength(255)]
        public string? ImageFileName { get; set; }

        // �Ϥ�MIME����
        [StringLength(50)]
        public string? ImageContentType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [Required]
        public bool IsVisible { get; set; } = true;

        // �O�_�m���]�u������^
        public bool IsPinned { get; set; } = false;

        // �޲z���^��
        [StringLength(500)]
        public string? AdminReply { get; set; }
        public DateTime? AdminReplyTime { get; set; }
        public string? AdminRepliedBy { get; set; }

        // �^�Ь����]�Ω�����^�Х\��^
        public int? ReplyId { get; set; }

        // �������Ωʧ벼
        public int HelpfulCount { get; set; } = 0;
        public int UnhelpfulCount { get; set; } = 0;

        // �ɯ��ݩ�
        public virtual User User { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
        public virtual Review? ReplyTo { get; set; }
        public virtual ICollection<Review> Replies { get; set; } = new List<Review>();
        public virtual ICollection<ReviewReport> Reports { get; set; } = new List<ReviewReport>();

        // �p���ݩ�
        [NotMapped]
        public bool IsReply => ReplyId.HasValue;

        [NotMapped]
        public string DisplayUserName => UserName ?? User?.Username ?? "�ΦW�Τ�";

        [NotMapped]
        public string RatingStars
        {
            get
            {
                var stars = "";
                for (int i = 1; i <= 5; i++)
                {
                    stars += i <= Rating ? "��" : "��";
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
                    return $"{(int)timeSpan.TotalDays} �ѫe";
                if (timeSpan.TotalHours >= 1)
                    return $"{(int)timeSpan.TotalHours} �p�ɫe";
                if (timeSpan.TotalMinutes >= 1)
                    return $"{(int)timeSpan.TotalMinutes} �����e";
                return "���";
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
            5 => "�D�`���N",
            4 => "���N",
            3 => "���q",
            2 => "�����N",
            1 => "�D�`�����N",
            _ => "������"
        };
    }
}