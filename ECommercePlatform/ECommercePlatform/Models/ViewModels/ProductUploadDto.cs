using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models.ViewModels
{
    public class ProductUploadDto
    {
        [Required(ErrorMessage = "產品名稱是必填的")]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "價格必須大於 0")]
        public decimal Price { get; set; }

        public decimal? DiscountPrice { get; set; }
        public DateTime? DiscountStart { get; set; }
        public DateTime? DiscountEnd { get; set; }

        // 添加缺少的 ImageUrl 屬性
        public string? ImageUrl { get; set; }

        public IFormFile? ImageFile { get; set; }

        // 添加缺少的 Stock 屬性
        [Range(0, int.MaxValue, ErrorMessage = "庫存不能為負數")]
        public int? Stock { get; set; }
    }
}