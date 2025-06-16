using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models
{
    //庫存預留模型
    //用於在用戶下單過程中暫時預留庫存，防止超賣
    public class StockReservation
    {
        //預留ID（使用GUID確保唯一性）
        [Key]
        [StringLength(50)]
        public string Id { get; set; } = string.Empty;
        //商品ID
        [Required]
        public int ProductId { get; set; }
        //預留數量
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "預留數量必須大於0")]
        public int Quantity { get; set; }
        //預留建立時間
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        //預留過期時間（通常為15分鐘後）
        [Required]
        public DateTime ExpiresAt { get; set; }
        //是否已確認（下單成功後設為true）
        public bool IsConfirmed { get; set; } = false;
        //確認時間
        public DateTime? ConfirmedAt { get; set; }
        //關聯的商品
        public virtual Product Product { get; set; } = null!;
        //檢查預留是否已過期
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
        //檢查預留是否有效（未過期且未確認）
        public bool IsActive => !IsExpired && !IsConfirmed;
    }
}