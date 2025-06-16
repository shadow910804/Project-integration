using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace ECommercePlatform.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "產品名稱是必填的。")]
        public string Name { get; set; } = null!;

        public string? Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "價格必須大於 0。")]
        [Column(TypeName = "decimal(18, 2)")]
        public decimal Price { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal? DiscountPrice { get; set; }

        public DateTime? DiscountStart { get; set; }
        public DateTime? DiscountEnd { get; set; }

        // 🆕 加入庫存屬性
        [Range(0, int.MaxValue, ErrorMessage = "庫存不能為負數")]
        public int Stock { get; set; } = 100; // 預設庫存 100

        public string? ImageUrl { get; set; }
        public byte[]? ImageData { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsActive { get; set; } = true;

        // 導航屬性
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        public ICollection<Review> Reviews { get; set; } = new List<Review>();

        //計算屬性：平均評分
        [NotMapped]
        public decimal? AverageRating
        {
            get
            {
                if (Reviews == null || !Reviews.Any())
                    return null;
                return Math.Round((decimal)Reviews.Where(r => r.IsVisible).Average(r => r.Rating), 1);
            }
        }

        //計算屬性：目前有效價格
        [NotMapped]
        public decimal CurrentPrice
        {
            get
            {
                // 檢查是否在折扣期間
                if (DiscountPrice.HasValue &&
                    DiscountStart.HasValue && DiscountEnd.HasValue &&
                    DateTime.Now >= DiscountStart.Value &&
                    DateTime.Now <= DiscountEnd.Value)
                {
                    return DiscountPrice.Value;
                }
                return Price;
            }
        }

        //計算屬性：是否有折扣
        [NotMapped]
        public bool HasDiscount
        {
            get
            {
                return DiscountPrice.HasValue &&
                       DiscountStart.HasValue && DiscountEnd.HasValue &&
                       DateTime.Now >= DiscountStart.Value &&
                       DateTime.Now <= DiscountEnd.Value &&
                       DiscountPrice < Price;
            }
        }
    }
}