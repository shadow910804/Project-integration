namespace ECommercePlatform.Services
{
    // 訂單管理服務接口
    // 提供完整的訂單生命週期管理功能
    public interface IOrderService
    {
        // 創建訂單
        // 整合庫存檢查、預留、扣減等完整流程
        Task<OrderResult> CreateOrderAsync(CreateOrderRequest request);

        // 更新訂單狀態
        Task<bool> UpdateOrderStatusAsync(int orderId, string newStatus, int? updatedBy = null);

        // 取消訂單
        // 包含庫存恢復、退款處理等
        Task<bool> CancelOrderAsync(int orderId, string reason, int? cancelledBy = null);

        // 獲取訂單詳情
        Task<OrderDetailsDto?> GetOrderDetailsAsync(int orderId, int? userId = null);

        // 獲取用戶訂單列表
        Task<List<OrderSummaryDto>> GetUserOrdersAsync(int userId, int page = 1, int pageSize = 10, string? status = null);

        // 確認付款
        Task<bool> ConfirmPaymentAsync(int orderId, string paymentReference);

        // 處理退款
        Task<bool> ProcessRefundAsync(int orderId, decimal amount, string reason);

        // 獲取訂單統計數據
        Task<OrderStatistics> GetOrderStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null);

        // 批量更新訂單狀態
        Task<int> BatchUpdateOrderStatusAsync(List<int> orderIds, string newStatus, int updatedBy);

        // 檢查訂單是否可以取消
        Task<(bool CanCancel, string Reason)> CanCancelOrderAsync(int orderId);

        // 檢查訂單是否可以退款
        Task<(bool CanRefund, string Reason)> CanRefundOrderAsync(int orderId);
    }

    // 創建訂單請求
    public class CreateOrderRequest
    {
        public int UserId { get; set; }
        public string ShippingAddress { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string ShippingMethod { get; set; } = "standard";
        public string? Notes { get; set; }
        public string? CouponCode { get; set; }
    }

    // 訂單創建結果
    public class OrderResult
    {
        public bool IsSuccess { get; set; }
        public int? OrderId { get; set; }
        public string Message { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = [];

        public static OrderResult Success(int orderId, string message = "訂單創建成功")
        {
            return new OrderResult
            {
                IsSuccess = true,
                OrderId = orderId,
                Message = message
            };
        }

        public static OrderResult Failure(string message, List<string>? errors = null)
        {
            return new OrderResult
            {
                IsSuccess = false,
                Message = message,
                Errors = errors ?? []
            };
        }
    }

    // 訂單詳情DTO
    public class OrderDetailsDto
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public bool PaymentVerified { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public List<OrderItemDto> Items { get; set; } = new();
        public string? Notes { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal Subtotal => Items.Sum(i => i.Subtotal);
        public bool CanCancel => OrderStatus == "待處理" || OrderStatus == "處理中";
        public bool CanRefund => OrderStatus == "已送達" && PaymentVerified;
    }

    // 訂單項目DTO
    public class OrderItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImageUrl { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public bool HasDiscount { get; set; }
    }

    // 訂單摘要DTO
    public class OrderSummaryDto
    {
        public int Id { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public bool PaymentVerified { get; set; }
        public string StatusColor => OrderStatus switch
        {
            "待處理" => "warning",
            "處理中" => "info",
            "已發貨" => "primary",
            "已送達" => "success",
            "已取消" => "danger",
            "退款處理中" => "secondary",
            _ => "secondary"
        };
    }

    // 訂單統計數據
    public class OrderStatistics
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public Dictionary<string, int> OrdersByStatus { get; set; } = [];
        public Dictionary<string, int> OrdersByPaymentMethod { get; set; } = [];
        public List<DailyOrderCount> DailyTrends { get; set; } = [];
        public List<TopSellingProduct> TopProducts { get; set; } = [];
    }

    // 每日訂單數量
    public class DailyOrderCount
    {
        public DateTime Date { get; set; }
        public int OrderCount { get; set; }
        public decimal Revenue { get; set; }
    }

    // 熱銷商品
    public class TopSellingProduct
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int QuantitySold { get; set; }
        public decimal Revenue { get; set; }
    }
}