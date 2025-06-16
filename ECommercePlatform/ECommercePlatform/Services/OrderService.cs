using ECommercePlatform.Data;
using ECommercePlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommercePlatform.Services
{
    // 訂單管理服務實現
    public class OrderService : IOrderService
    {
        private readonly ApplicationDbContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly EmailService _emailService;
        private readonly OperationLogService _logService;

        public OrderService(
            ApplicationDbContext context,
            IInventoryService inventoryService,
            EmailService emailService,
            OperationLogService logService)
        {
            _context = context;
            _inventoryService = inventoryService;
            _emailService = emailService;
            _logService = logService;
        }

        // 創建訂單（完整流程）
        public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. 驗證用戶
                var user = await _context.Users.FindAsync(request.UserId);
                if (user == null || !user.IsActive)
                {
                    return OrderResult.Failure("用戶不存在或已停用");
                }

                // 2. 獲取購物車商品
                var cartItems = await _context.CartItems
                    .Include(c => c.Product)
                    .Where(c => c.UserId == request.UserId)
                    .ToListAsync();

                if (cartItems.Count == 0)
                {
                    return OrderResult.Failure("購物車為空");
                }

                // 3. 檢查商品狀態和庫存
                var reservationId = Guid.NewGuid().ToString();
                var orderItems = new List<OrderItem>();
                decimal totalAmount = 0;

                foreach (var cartItem in cartItems)
                {
                    // 檢查商品是否有效
                    if (!cartItem.Product.IsActive)
                    {
                        return OrderResult.Failure($"商品 {cartItem.Product.Name} 已下架");
                    }

                    // 檢查庫存
                    var availability = await _inventoryService.CheckAvailabilityAsync(
                        cartItem.ProductId, cartItem.Quantity);

                    if (!availability.IsAvailable)
                    {
                        return OrderResult.Failure($"商品 {cartItem.Product.Name} 庫存不足");
                    }

                    // 預留庫存
                    var itemReservationId = $"{reservationId}_{cartItem.ProductId}";
                    var reserved = await _inventoryService.ReserveStockAsync(
                        cartItem.ProductId, cartItem.Quantity, itemReservationId);

                    if (!reserved)
                    {
                        return OrderResult.Failure($"無法預留商品 {cartItem.Product.Name} 的庫存");
                    }

                    // 計算價格（考慮折扣）
                    var unitPrice = cartItem.Product.CurrentPrice;
                    var itemTotal = unitPrice * cartItem.Quantity;
                    totalAmount += itemTotal;

                    orderItems.Add(new OrderItem
                    {
                        ProductId = cartItem.ProductId,
                        Quantity = cartItem.Quantity,
                        UnitPrice = unitPrice
                    });
                }

                // 4. 計算運費
                var shippingCost = CalculateShippingCost(totalAmount, request.ShippingMethod);
                totalAmount += shippingCost;

                // 5. 創建訂單
                var order = new Order
                {
                    UserId = request.UserId,
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = totalAmount,
                    ShippingAddress = request.ShippingAddress,
                    PaymentMethod = request.PaymentMethod,
                    OrderStatus = "待處理",
                    PaymentVerified = false,
                    OrderItems = orderItems
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // 6. 確認庫存預留
                foreach (var cartItem in cartItems)
                {
                    var itemReservationId = $"{reservationId}_{cartItem.ProductId}";
                    await _inventoryService.ConfirmReservationAsync(itemReservationId);
                }

                // 7. 清空購物車
                _context.CartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // 8. 發送訂單確認郵件
                try
                {
                    await _emailService.SendOrderConfirmationAsync(user.Email, order, user.Username);
                }
                catch (Exception ex)
                {
                    _logService.Log("Order", "EmailError", order.Id.ToString(),
                        $"訂單確認郵件發送失敗: {ex.Message}");
                }

                // 9. 記錄操作日誌
                _logService.Log("Order", "Create", order.Id.ToString(),
                    $"創建訂單，總金額: {totalAmount:C}");

                return OrderResult.Success(order.Id, "訂單創建成功");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logService.Log("Order", "CreateError", "", ex.Message);
                return OrderResult.Failure($"創建訂單失敗: {ex.Message}");
            }
        }

        // 更新訂單狀態
        public async Task<bool> UpdateOrderStatusAsync(int orderId, string newStatus, int? updatedBy = null)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null) return false;

                var oldStatus = order.OrderStatus;
                order.OrderStatus = newStatus;

                await _context.SaveChangesAsync();

                // 發送狀態更新郵件
                try
                {
                    await _emailService.SendOrderStatusUpdateAsync(
                        order.User.Email, order, order.User.Username, newStatus);
                }
                catch (Exception ex)
                {
                    _logService.Log("Order", "StatusEmailError", orderId.ToString(),
                        $"狀態更新郵件發送失敗: {ex.Message}");
                }

                // 記錄操作日誌
                _logService.Log("Order", "StatusUpdate", orderId.ToString(),
                    $"狀態變更: {oldStatus} → {newStatus}");

                return true;
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "StatusUpdateError", orderId.ToString(), ex.Message);
                return false;
            }
        }

        // 取消訂單
        public async Task<bool> CancelOrderAsync(int orderId, string reason, int? cancelledBy = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null) return false;

                // 檢查是否可以取消
                if (order.OrderStatus == "已發貨" || order.OrderStatus == "已送達")
                {
                    return false; // 已發貨的訂單不能取消
                }

                // 恢復庫存
                foreach (var item in order.OrderItems)
                {
                    await _inventoryService.AdjustStockAsync(
                        item.ProductId, item.Quantity,
                        $"訂單取消恢復庫存，訂單ID: {orderId}", cancelledBy);
                }

                // 更新訂單狀態
                order.OrderStatus = "已取消";
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // 發送取消通知
                try
                {
                    await _emailService.SendOrderStatusUpdateAsync(
                        order.User.Email, order, order.User.Username, "已取消");
                }
                catch (Exception ex)
                {
                    _logService.Log("Order", "CancelEmailError", orderId.ToString(),
                        $"取消通知郵件發送失敗: {ex.Message}");
                }

                _logService.Log("Order", "Cancel", orderId.ToString(),
                    $"訂單取消，原因: {reason}");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logService.Log("Order", "CancelError", orderId.ToString(), ex.Message);
                return false;
            }
        }

        // 獲取訂單詳情
        public async Task<OrderDetailsDto?> GetOrderDetailsAsync(int orderId, int? userId = null)
        {
            var query = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .AsQueryable();

            if (userId.HasValue)
            {
                query = query.Where(o => o.UserId == userId.Value);
            }

            var order = await query.FirstOrDefaultAsync(o => o.Id == orderId);
            if (order == null) return null;

            return new OrderDetailsDto
            {
                Id = order.Id,
                OrderDate = order.OrderDate,
                TotalAmount = order.TotalAmount,
                OrderStatus = order.OrderStatus,
                PaymentMethod = order.PaymentMethod,
                ShippingAddress = order.ShippingAddress,
                PaymentVerified = order.PaymentVerified,
                CustomerName = order.User.Username,
                CustomerEmail = order.User.Email,
                Items = order.OrderItems.Select(oi => new OrderItemDto
                {
                    ProductId = oi.ProductId,
                    ProductName = oi.Product.Name,
                    ProductImageUrl = oi.Product.ImageUrl,
                    Quantity = oi.Quantity,
                    UnitPrice = oi.UnitPrice,
                    Subtotal = oi.Quantity * oi.UnitPrice
                }).ToList()
            };
        }

        // 獲取用戶訂單列表
        public async Task<List<OrderSummaryDto>> GetUserOrdersAsync(int userId, int page = 1, int pageSize = 10, string? status = null)
        {
            var query = _context.Orders
                .Where(o => o.UserId == userId)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(o => o.OrderStatus == status);
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new OrderSummaryDto
                {
                    Id = o.Id,
                    OrderDate = o.OrderDate,
                    TotalAmount = o.TotalAmount,
                    OrderStatus = o.OrderStatus,
                    PaymentMethod = o.PaymentMethod,
                    ItemCount = o.OrderItems.Count(),
                    PaymentVerified = o.PaymentVerified
                })
                .ToListAsync();

            return orders;
        }

        // 確認付款
        public async Task<bool> ConfirmPaymentAsync(int orderId, string paymentReference)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null) return false;

                order.PaymentVerified = true;

                // 如果是待處理狀態，自動改為處理中
                if (order.OrderStatus == "待處理")
                {
                    order.OrderStatus = "處理中";
                }

                await _context.SaveChangesAsync();

                _logService.Log("Order", "PaymentConfirm", orderId.ToString(),
                    $"付款確認，參考號: {paymentReference}");

                return true;
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "PaymentConfirmError", orderId.ToString(), ex.Message);
                return false;
            }
        }

        // 處理退款
        public async Task<bool> ProcessRefundAsync(int orderId, decimal amount, string reason)
        {
            try
            {
                _logService.Log("Order", "Refund", orderId.ToString(),
                    $"退款處理，金額: {amount:C}，原因: {reason}");

                // 實際退款邏輯...
                // await _paymentGateway.ProcessRefundAsync(orderId, amount);

                return true;
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "RefundError", orderId.ToString(), ex.Message);
                return false;
            }
        }

        // 獲取訂單統計數據
        public async Task<OrderStatistics> GetOrderStatisticsAsync(DateTime? fromDate = null, DateTime? toDate = null)
        {
            fromDate ??= DateTime.UtcNow.AddDays(-30);
            toDate ??= DateTime.UtcNow;

            var query = _context.Orders
                .Where(o => o.OrderDate >= fromDate && o.OrderDate <= toDate);

            var orders = await query.ToListAsync();

            var statistics = new OrderStatistics
            {
                TotalOrders = orders.Count,
                TotalRevenue = orders.Sum(o => o.TotalAmount),
                AverageOrderValue = orders.Count > 0 ? orders.Average(o => o.TotalAmount) : 0,
                OrdersByStatus = orders.GroupBy(o => o.OrderStatus ?? "未知")
                    .ToDictionary(g => g.Key, g => g.Count()),
                OrdersByPaymentMethod = orders.GroupBy(o => o.PaymentMethod ?? "未知")
                    .ToDictionary(g => g.Key, g => g.Count()),
                DailyTrends = orders.GroupBy(o => o.OrderDate.Date)
                    .Select(g => new DailyOrderCount
                    {
                        Date = g.Key,
                        OrderCount = g.Count(),
                        Revenue = g.Sum(o => o.TotalAmount)
                    })
                    .OrderBy(d => d.Date)
                    .ToList()
            };

            return statistics;
        }

        // 批量更新訂單狀態
        public async Task<int> BatchUpdateOrderStatusAsync(List<int> orderIds, string newStatus, int updatedBy)
        {
            try
            {
                var orders = await _context.Orders
                    .Where(o => orderIds.Contains(o.Id))
                    .ToListAsync();

                foreach (var order in orders)
                {
                    order.OrderStatus = newStatus;
                }

                await _context.SaveChangesAsync();

                _logService.Log("Order", "BatchUpdate", string.Join(",", orderIds),
                    $"批量更新狀態為: {newStatus}，共 {orders.Count} 筆");

                return orders.Count;
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "BatchUpdateError", "", ex.Message);
                return 0;
            }
        }

        // 檢查訂單是否可以取消
        public async Task<(bool CanCancel, string Reason)> CanCancelOrderAsync(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return (false, "訂單不存在");
            }

            return order.OrderStatus switch
            {
                "已發貨" => (false, "訂單已發貨，無法取消"),
                "已送達" => (false, "訂單已送達，無法取消"),
                "已取消" => (false, "訂單已取消"),
                _ => (true, "可以取消")
            };
        }

        // 檢查訂單是否可以退款
        public async Task<(bool CanRefund, string Reason)> CanRefundOrderAsync(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null)
            {
                return (false, "訂單不存在");
            }

            if (order.OrderStatus != "已送達")
            {
                return (false, "只有已送達的訂單才能申請退款");
            }

            if (!order.PaymentVerified)
            {
                return (false, "訂單未付款，無法退款");
            }

            // 檢查退款期限（7天）
            if (DateTime.UtcNow.Subtract(order.OrderDate).TotalDays > 7)
            {
                return (false, "已超過退款期限（7天）");
            }

            return (true, "可以申請退款");
        }

        // 計算運費
        private decimal CalculateShippingCost(decimal orderAmount, string shippingMethod = "standard")
        {
            // 滿千免運
            if (orderAmount >= 1000) return 0;

            return shippingMethod.ToLower() switch
            {
                "express" => 150,
                "standard" => 100,
                _ => 100
            };
        }
    }
}