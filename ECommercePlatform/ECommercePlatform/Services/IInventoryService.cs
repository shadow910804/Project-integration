using ECommercePlatform.Models;

namespace ECommercePlatform.Services
{
    //庫存管理服務接口
    //提供完整的庫存管理功能，包括庫存檢查、預留、確認、調整等
    public interface IInventoryService
    {
        //檢查商品庫存可用性
        Task<InventoryCheckResult> CheckAvailabilityAsync(int productId, int requestedQuantity);
        //預留庫存
        //在用戶下單過程中暫時預留庫存，防止超賣
        Task<bool> ReserveStockAsync(int productId, int quantity, string reservationId);
        //確認庫存預留（下單成功後確認扣減庫存）
        Task<bool> ConfirmReservationAsync(string reservationId);
        //釋放庫存預留（取消訂單或預留過期時）
        Task<bool> ReleaseReservationAsync(string reservationId);
        /// 調整商品庫存 用於進貨、退貨、損耗等庫存調整
        Task<bool> AdjustStockAsync(int productId, int adjustment, string reason, int? userId = null);
        /// 獲取低庫存商品列表
        Task<List<LowStockAlert>> GetLowStockProductsAsync(int threshold = 5);
        /// 獲取商品庫存變動歷史
        Task<StockHistory> GetStockHistoryAsync(int productId, DateTime? fromDate = null);
        /// 清理過期的庫存預留 由背景服務定期調用
        Task CleanupExpiredReservationsAsync();
        /// 批量檢查多個商品的庫存狀態
        Task<Dictionary<int, InventoryCheckResult>> BatchCheckAvailabilityAsync(Dictionary<int, int> items);
        /// 獲取商品的實際可用庫存（扣除預留量）
        Task<int> GetAvailableStockAsync(int productId);
        /// 獲取商品的預留庫存總量
        Task<int> GetReservedStockAsync(int productId);
    }

    //庫存檢查結果
    public class InventoryCheckResult
    {
        //是否有足夠庫存
        public bool IsAvailable { get; set; }
        //可用庫存數量
        public int AvailableQuantity { get; set; }
        //錯誤訊息（當庫存不足時）
        public string ErrorMessage { get; set; } = string.Empty;
        //商品名稱（用於錯誤訊息）
        public string ProductName { get; set; } = string.Empty;
        //創建成功結果
        public static InventoryCheckResult Success(int availableQuantity)
        {
            return new InventoryCheckResult
            {
                IsAvailable = true,
                AvailableQuantity = availableQuantity
            };
        }

        //創建失敗結果
        public static InventoryCheckResult Failure(string errorMessage, int availableQuantity = 0)
        {
            return new InventoryCheckResult
            {
                IsAvailable = false,
                ErrorMessage = errorMessage,
                AvailableQuantity = availableQuantity
            };
        }
    }

    //低庫存警報
    public class LowStockAlert
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        //目前庫存數量
        public int CurrentStock { get; set; }
        //低庫存閾值
        public int Threshold { get; set; }
        //預留庫存數量
        public int ReservedStock { get; set; }
        //實際可用庫存
        public int AvailableStock => CurrentStock - ReservedStock;
        //最後更新時間
        public DateTime LastUpdated { get; set; }
        //緊急程度（0-100，數值越高越緊急）
        public int UrgencyLevel => CurrentStock <= 0 ? 100 :
                                  CurrentStock <= Threshold / 2 ? 80 :
                                  CurrentStock <= Threshold ? 60 : 0;
    }

    //庫存歷史記錄
    public class StockHistory
    {
        public int ProductId { get; set; }
        //商品名稱
        public string ProductName { get; set; } = string.Empty;
        //目前庫存數量
        public int CurrentStock { get; set; }
        //庫存變動記錄列表
        public List<StockMovement> Movements { get; set; } = new();
        //查詢起始日期
        public DateTime FromDate { get; set; }
        //查詢結束日期
        public DateTime ToDate { get; set; }
        //總入庫數量
        public int TotalInbound => Movements.Where(m => m.Quantity > 0).Sum(m => m.Quantity);
        //總出庫數量
        public int TotalOutbound => Math.Abs(Movements.Where(m => m.Quantity < 0).Sum(m => m.Quantity));
        //淨變動數量
        public int NetMovement => TotalInbound - TotalOutbound;
    }
}