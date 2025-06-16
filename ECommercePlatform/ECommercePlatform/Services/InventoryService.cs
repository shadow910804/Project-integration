using ECommercePlatform.Data;
using ECommercePlatform.Models;
using Microsoft.EntityFrameworkCore;

namespace ECommercePlatform.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly OperationLogService _logService;

        public InventoryService(ApplicationDbContext context, EmailService emailService, OperationLogService logService)
        {
            _context = context;
            _emailService = emailService;
            _logService = logService;
        }

        //檢查庫存可用性
        public async Task<InventoryCheckResult> CheckAvailabilityAsync(int productId, int requestedQuantity)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                return InventoryCheckResult.Failure("商品不存在");
            }

            if (!product.IsActive)
            {
                return InventoryCheckResult.Failure("商品已下架");
            }

            //計算可用庫存（總庫存 - 已預留庫存）
            var reservedStock = await GetReservedStockAsync(productId);
            var availableStock = product.Stock - reservedStock;

            if (availableStock < requestedQuantity)
            {
                return InventoryCheckResult.Failure(
                    $"庫存不足，可用數量：{availableStock}",
                    Math.Max(0, availableStock));
            }

            return InventoryCheckResult.Success(availableStock);
        }

        //預留庫存
        public async Task<bool> ReserveStockAsync(int productId, int quantity, string reservationId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 檢查可用性
                var availability = await CheckAvailabilityAsync(productId, quantity);
                if (!availability.IsAvailable)
                {
                    return false;
                }

                // 檢查是否已存在相同的預留ID
                var existingReservation = await _context.StockReservations
                    .FirstOrDefaultAsync(r => r.Id == reservationId);

                if (existingReservation != null)
                {
                    return false; // 預留ID已存在
                }

                // 創建預留記錄
                var reservation = new StockReservation
                {
                    Id = reservationId,
                    ProductId = productId,
                    Quantity = quantity,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(15), // 15分鐘後過期
                    IsConfirmed = false
                };

                _context.StockReservations.Add(reservation);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logService.Log("Inventory", "Reserve", productId.ToString(),
                    $"預留庫存 {quantity} 件，預留ID: {reservationId}");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logService.Log("Inventory", "ReserveError", productId.ToString(), ex.Message);
                return false;
            }
        }

        //確認預留（下單成功後確認扣減庫存）
        public async Task<bool> ConfirmReservationAsync(string reservationId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var reservation = await _context.StockReservations
                    .Include(r => r.Product)
                    .FirstOrDefaultAsync(r => r.Id == reservationId && !r.IsConfirmed);

                if (reservation == null || reservation.ExpiresAt < DateTime.UtcNow)
                {
                    return false;
                }

                // 確認預留並扣減實際庫存
                reservation.IsConfirmed = true;
                reservation.ConfirmedAt = DateTime.UtcNow;

                var previousStock = reservation.Product.Stock;
                reservation.Product.Stock -= reservation.Quantity;

                // 記錄庫存變動
                var movement = new StockMovement
                {
                    ProductId = reservation.ProductId,
                    MovementType = StockMovementType.Sale,
                    Quantity = -reservation.Quantity,
                    PreviousStock = previousStock,
                    NewStock = reservation.Product.Stock,
                    Reason = $"訂單確認，預留ID: {reservationId}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.StockMovements.Add(movement);
                await _context.SaveChangesAsync();

                // 檢查是否需要發送低庫存警告
                await CheckAndSendLowStockAlertAsync(reservation.Product);

                await transaction.CommitAsync();

                _logService.Log("Inventory", "Confirm", reservation.ProductId.ToString(),
                    $"確認庫存扣減 {reservation.Quantity} 件，預留ID: {reservationId}");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logService.Log("Inventory", "ConfirmError", reservationId, ex.Message);
                return false;
            }
        }

        //釋放預留
        public async Task<bool> ReleaseReservationAsync(string reservationId)
        {
            try
            {
                var reservation = await _context.StockReservations
                    .FirstOrDefaultAsync(r => r.Id == reservationId && !r.IsConfirmed);

                if (reservation != null)
                {
                    _context.StockReservations.Remove(reservation);
                    await _context.SaveChangesAsync();

                    _logService.Log("Inventory", "Release", reservation.ProductId.ToString(),
                        $"釋放預留庫存 {reservation.Quantity} 件，預留ID: {reservationId}");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logService.Log("Inventory", "ReleaseError", reservationId, ex.Message);
                return false;
            }
        }

        //調整庫存
        public async Task<bool> AdjustStockAsync(int productId, int adjustment, string reason, int? userId = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _context.Products.FindAsync(productId);
                if (product == null) return false;

                var previousStock = product.Stock;
                product.Stock += adjustment;

                // 確保庫存不會變成負數
                if (product.Stock < 0)
                {
                    product.Stock = 0;
                }

                // 記錄庫存變動
                var movementType = adjustment > 0 ? StockMovementType.Adjustment_In :
                                  adjustment < 0 ? StockMovementType.Adjustment_Out :
                                  StockMovementType.Other;

                var movement = new StockMovement
                {
                    ProductId = productId,
                    MovementType = movementType,
                    Quantity = adjustment,
                    PreviousStock = previousStock,
                    NewStock = product.Stock,
                    Reason = reason,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.StockMovements.Add(movement);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logService.Log("Inventory", "Adjust", productId.ToString(),
                    $"庫存調整：{adjustment:+#;-#;0}，原因：{reason}");

                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logService.Log("Inventory", "AdjustError", productId.ToString(), ex.Message);
                return false;
            }
        }

        //獲取低庫存商品列表
        public async Task<List<LowStockAlert>> GetLowStockProductsAsync(int threshold = 5)
        {
            var lowStockProducts = await _context.Products
                .Where(p => p.IsActive && p.Stock <= threshold)
                .ToListAsync();

            var alerts = new List<LowStockAlert>();

            foreach (var product in lowStockProducts)
            {
                var reservedStock = await GetReservedStockAsync(product.Id);

                alerts.Add(new LowStockAlert
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    CurrentStock = product.Stock,
                    ReservedStock = reservedStock,
                    Threshold = threshold,
                    LastUpdated = DateTime.UtcNow
                });
            }

            return alerts.OrderBy(a => a.AvailableStock).ToList();
        }

        //獲取商品庫存歷史
        public async Task<StockHistory> GetStockHistoryAsync(int productId, DateTime? fromDate = null)
        {
            fromDate ??= DateTime.UtcNow.AddDays(-30); // 預設查詢30天

            var movements = await _context.StockMovements
                .Where(m => m.ProductId == productId && m.CreatedAt >= fromDate)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            var product = await _context.Products.FindAsync(productId);

            return new StockHistory
            {
                ProductId = productId,
                ProductName = product?.Name ?? "Unknown",
                CurrentStock = product?.Stock ?? 0,
                Movements = movements,
                FromDate = fromDate.Value,
                ToDate = DateTime.UtcNow
            };
        }

        //清理過期的庫存預留
        public async Task CleanupExpiredReservationsAsync()
        {
            try
            {
                var expiredReservations = await _context.StockReservations
                    .Where(r => r.ExpiresAt < DateTime.UtcNow && !r.IsConfirmed)
                    .ToListAsync();

                if (expiredReservations.Any())
                {
                    _context.StockReservations.RemoveRange(expiredReservations);
                    await _context.SaveChangesAsync();

                    _logService.Log("Inventory", "Cleanup", "",
                        $"清理過期預留 {expiredReservations.Count} 筆");
                }
            }
            catch (Exception ex)
            {
                _logService.Log("Inventory", "CleanupError", "", ex.Message);
            }
        }

        //批量檢查多個商品的庫存狀態
        public async Task<Dictionary<int, InventoryCheckResult>> BatchCheckAvailabilityAsync(Dictionary<int, int> items)
        {
            var results = new Dictionary<int, InventoryCheckResult>();

            foreach (var item in items)
            {
                var result = await CheckAvailabilityAsync(item.Key, item.Value);
                results[item.Key] = result;
            }

            return results;
        }

        //獲取商品的實際可用庫存
        public async Task<int> GetAvailableStockAsync(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            if (product == null) return 0;

            var reservedStock = await GetReservedStockAsync(productId);
            return Math.Max(0, product.Stock - reservedStock);
        }

        //獲取商品的預留庫存總量
        public async Task<int> GetReservedStockAsync(int productId)
        {
            return await _context.StockReservations
                .Where(r => r.ProductId == productId &&
                           r.ExpiresAt > DateTime.UtcNow &&
                           !r.IsConfirmed)
                .SumAsync(r => r.Quantity);
        }

        //檢查並發送低庫存警告
        private async Task CheckAndSendLowStockAlertAsync(Product product)
        {
            const int lowStockThreshold = 5;

            if (product.Stock <= lowStockThreshold)
            {
                try
                {
                    await _emailService.SendLowStockAlertAsync(product.Name, product.Stock, lowStockThreshold);
                }
                catch (Exception ex)
                {
                    // 郵件發送失敗不影響庫存操作
                    _logService.Log("Inventory", "EmailError", product.Id.ToString(), ex.Message);
                }
            }
        }
    }
}