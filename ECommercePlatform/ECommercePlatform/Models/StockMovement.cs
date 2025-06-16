using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models
{
    // 庫存變動記錄模型
    // 記錄所有庫存變動的歷史，用於追蹤和審計
    public class StockMovement
    {
        // 記錄ID
        [Key]
        public int Id { get; set; }

        // 商品ID
        [Required]
        public int ProductId { get; set; }

        // 變動類型
        [Required]
        public StockMovementType MovementType { get; set; }

        // 變動數量（正數表示增加，負數表示減少）
        [Required]
        public int Quantity { get; set; }

        // 變動前庫存數量
        [Required]
        public int PreviousStock { get; set; }

        // 變動後庫存數量
        [Required]
        public int NewStock { get; set; }

        // 變動原因說明
        [Required]
        [StringLength(500)]
        public string Reason { get; set; } = string.Empty;

        // 操作用戶ID（可選，系統自動操作時為空）
        public int? UserId { get; set; }

        // 變動時間
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 關聯的商品
        public virtual Product Product { get; set; } = null!;

        // 關聯的操作用戶
        public virtual User? User { get; set; }

        // 是否為庫存增加
        public bool IsStockIncrease => Quantity > 0;

        // 是否為庫存減少
        public bool IsStockDecrease => Quantity < 0;

        // 獲取變動數量的絕對值
        public int AbsoluteQuantity => Math.Abs(Quantity);

        // 獲取變動類型的顯示名稱
        public string MovementTypeDisplay => MovementType switch
        {
            StockMovementType.Sale => "銷售",
            StockMovementType.Return => "退貨",
            StockMovementType.Adjustment_In => "入庫調整",
            StockMovementType.Adjustment_Out => "出庫調整",
            StockMovementType.Damage => "損耗",
            StockMovementType.Transfer => "調撥",
            StockMovementType.Other => "其他",
            _ => "未知"
        };
    }
}