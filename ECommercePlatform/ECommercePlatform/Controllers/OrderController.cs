using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Models.ViewModels;
using ECommercePlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Web;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace ECommercePlatform.Controllers
{
    [Authorize(Roles = "User")]
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderService _orderService;
        private readonly OperationLogService _logService;

        public OrderController(
            ApplicationDbContext context,
            IOrderService orderService,
            OperationLogService logService)
        {
            _context = context;
            _orderService = orderService;
            _logService = logService;
        }

        #region API 方法
        //API: 獲取用戶訂單
        [HttpGet("/api/orders/{userId}")]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        public async Task<IActionResult> GetUserOrdersApi(int userId)
        {
            // 檢查權限：只能查看自己的訂單或管理員查看
            var currentUserId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var userRole = User.Claims.FirstOrDefault(c => c.Type == "UserRole")?.Value ?? "";

            if (currentUserId != userId && userRole != "Admin" && userRole != "Engineer")
            {
                return Forbid();
            }

            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.TotalAmount,
                    o.OrderDate,
                    o.OrderStatus,
                    o.PaymentMethod,
                    Items = o.OrderItems.Select(i => new
                    {
                        i.Product.Name,
                        i.Quantity,
                        i.UnitPrice
                    })
                })
                .ToListAsync();

            return Json(new { success = true, data = orders });
        }

        //API: 創建訂單
        [HttpPost("/api/orders")]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        public async Task<IActionResult> CreateOrderApi([FromBody] CreateOrderApiRequest request)
        {
            try
            {
                var orderRequest = new CreateOrderRequest
                {
                    UserId = request.UserId,
                    ShippingAddress = request.ShippingAddress,
                    PaymentMethod = request.PaymentMethod,
                    ShippingMethod = request.ShippingMethod ?? "standard"
                };

                var result = await _orderService.CreateOrderAsync(orderRequest);

                if (result.IsSuccess)
                {
                    return Json(new
                    {
                        success = true,
                        orderId = result.OrderId,
                        message = result.Message
                    });
                }
                else
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = result.Message,
                        errors = result.Errors
                    });
                }
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "CreateApiError", "", ex.Message);
                return StatusCode(500, new { success = false, message = "創建訂單失敗" });
            }
        }
        #endregion

        // DTO 類別
        public class CreateOrderApiRequest
        {
            public int UserId { get; set; }
            public string ShippingAddress { get; set; } = string.Empty;
            public string PaymentMethod { get; set; } = string.Empty;
            public string? ShippingMethod { get; set; }
        }

        public IActionResult Checkout()
        {
            return View();
        }

        //我的訂單列表
        [HttpGet]
        [Route("orders")]
        public async Task<IActionResult> MyOrders(string? status = null, int page = 1)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
                const int pageSize = 10;

                var query = _context.Orders
                    .Where(o => o.UserId == userId)
                    .AsQueryable();

                // 狀態篩選
                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(o => o.OrderStatus == status);
                }

                var totalItems = await query.CountAsync();
                var orders = await query
                    .OrderByDescending(o => o.OrderDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(o => new OrderSummaryDto
                    {
                        Id = o.Id,
                        OrderDate = o.OrderDate,
                        TotalAmount = o.TotalAmount,
                        OrderStatus = o.OrderStatus ?? "未知", // 修正 Null 問題
                        PaymentMethod = o.PaymentMethod ?? "未指定", // 修正 Null 問題
                        ItemCount = o.OrderItems.Count(),
                        PaymentVerified = o.PaymentVerified
                    })
                    .ToListAsync();

                var result = new PagedResult<OrderSummaryDto>
                {
                    Items = orders,
                    TotalItems = totalItems,
                    PageNumber = page,
                    PageSize = pageSize
                };

                // 獲取訂單狀態統計
                var statusCounts = await _context.Orders
                    .Where(o => o.UserId == userId)
                    .GroupBy(o => o.OrderStatus ?? "未知") // 修正 Null 問題
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                ViewBag.StatusCounts = statusCounts.ToDictionary(x => x.Status, x => x.Count);
                ViewBag.CurrentStatus = status;

                return View(result);
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "MyOrdersError", "", ex.Message);
                TempData["Error"] = "載入訂單列表失敗";
                return View(new PagedResult<OrderSummaryDto>());
            }
        }

        //訂單詳情頁面
        [HttpGet]
        [Route("orders/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
                var orderDetails = await _orderService.GetOrderDetailsAsync(id, userId);

                if (orderDetails == null)
                {
                    TempData["Error"] = "訂單不存在或無權限查看";
                    return RedirectToAction("MyOrders");
                }

                // 獲取訂單操作歷史（如果需要）
                var orderHistory = await GetOrderHistoryAsync(id);
                ViewBag.OrderHistory = orderHistory;

                return View(orderDetails);
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "DetailsError", id.ToString(), ex.Message);
                TempData["Error"] = "載入訂單詳情失敗";
                return RedirectToAction("MyOrders");
            }
        }

        //取消訂單
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("orders/{id}/cancel")]
        public async Task<IActionResult> Cancel(int id, string? reason = null)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);

                // 檢查訂單是否屬於當前用戶
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

                if (order == null)
                {
                    return Json(new { success = false, message = "訂單不存在或無權限操作" });
                }

                // 檢查訂單狀態是否可以取消
                if (order.OrderStatus == "已發貨" || order.OrderStatus == "已送達" || order.OrderStatus == "已取消")
                {
                    return Json(new { success = false, message = "當前訂單狀態無法取消" });
                }

                var cancelReason = reason ?? "用戶主動取消";
                var success = await _orderService.CancelOrderAsync(id, cancelReason, userId);

                if (success)
                {
                    return Json(new { success = true, message = "訂單已成功取消" });
                }
                else
                {
                    return Json(new { success = false, message = "取消訂單失敗，請聯繫客服" });
                }
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "CancelError", id.ToString(), ex.Message);
                return Json(new { success = false, message = "系統錯誤，請稍後再試" });
            }
        }

        //確認收貨
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("orders/{id}/confirm-delivery")]
        public async Task<IActionResult> ConfirmDelivery(int id)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);

                // 檢查訂單是否屬於當前用戶且狀態為已發貨
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId && o.OrderStatus == "已發貨");

                if (order == null)
                {
                    return Json(new { success = false, message = "訂單不存在或狀態不正確" });
                }

                var success = await _orderService.UpdateOrderStatusAsync(id, "已送達", userId);

                if (success)
                {
                    return Json(new { success = true, message = "已確認收貨，感謝您的購買！" });
                }
                else
                {
                    return Json(new { success = false, message = "確認收貨失敗" });
                }
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "ConfirmDeliveryError", id.ToString(), ex.Message);
                return Json(new { success = false, message = "系統錯誤，請稍後再試" });
            }
        }

        //申請退款
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("orders/{id}/request-refund")]
        public async Task<IActionResult> RequestRefund(int id, string reason, decimal? amount = null)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);

                // 檢查訂單
                var order = await _context.Orders
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

                if (order == null)
                {
                    return Json(new { success = false, message = "訂單不存在" });
                }

                if (order.OrderStatus != "已送達")
                {
                    return Json(new { success = false, message = "只有已送達的訂單才能申請退款" });
                }

                // 檢查退款期限（例如：7天內）
                if (DateTime.UtcNow.Subtract(order.OrderDate).TotalDays > 7)
                {
                    return Json(new { success = false, message = "已超過退款期限（7天）" });
                }

                var refundAmount = amount ?? order.TotalAmount;
                var success = await _orderService.ProcessRefundAsync(id, refundAmount, reason);

                if (success)
                {
                    // 更新訂單狀態為退款處理中
                    await _orderService.UpdateOrderStatusAsync(id, "退款處理中", userId);
                    return Json(new { success = true, message = "退款申請已提交，我們會在3-5個工作日內處理" });
                }
                else
                {
                    return Json(new { success = false, message = "退款申請失敗，請聯繫客服" });
                }
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "RequestRefundError", id.ToString(), ex.Message);
                return Json(new { success = false, message = "系統錯誤，請稍後再試" });
            }
        }

        //重新購買（將訂單商品加回購物車）
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("orders/{id}/reorder")]
        public async Task<IActionResult> Reorder(int id)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

                if (order == null)
                {
                    return Json(new { success = false, message = "訂單不存在" });
                }

                var addedItems = new List<string>();
                var unavailableItems = new List<string>();

                foreach (var item in order.OrderItems)
                {
                    if (!item.Product.IsActive)
                    {
                        unavailableItems.Add(item.Product.Name);
                        continue;
                    }

                    // 檢查購物車中是否已有此商品
                    var existingCartItem = await _context.CartItems
                        .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == item.ProductId);

                    if (existingCartItem != null)
                    {
                        existingCartItem.Quantity += item.Quantity;
                        existingCartItem.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        var cartItem = new CartItem
                        {
                            UserId = userId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.CartItems.Add(cartItem);
                    }

                    addedItems.Add(item.Product.Name);
                }

                await _context.SaveChangesAsync();

                var message = $"已將 {addedItems.Count} 件商品加入購物車";
                if (unavailableItems.Any())
                {
                    message += $"，{unavailableItems.Count} 件商品已下架";
                }

                return Json(new
                {
                    success = true,
                    message = message,
                    addedCount = addedItems.Count,
                    unavailableCount = unavailableItems.Count
                });
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "ReorderError", id.ToString(), ex.Message);
                return Json(new { success = false, message = "重新購買失敗，請稍後再試" });
            }
        }

        //下載訂單發票/收據
        [HttpGet]
        [Route("orders/{id}/invoice")]
        public async Task<IActionResult> DownloadInvoice(int id)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
                var orderDetails = await _orderService.GetOrderDetailsAsync(id, userId);

                if (orderDetails == null)
                {
                    return NotFound();
                }

                // 生成簡單的 HTML 發票
                var html = GenerateInvoiceHtml(orderDetails);
                var bytes = System.Text.Encoding.UTF8.GetBytes(html);

                return File(bytes, "text/html", $"invoice_{id}.html");
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "DownloadInvoiceError", id.ToString(), ex.Message);
                return NotFound();
            }
        }

        //獲取訂單歷史記錄
        private async Task<List<OrderHistoryItem>> GetOrderHistoryAsync(int orderId)
        {
            var history = new List<OrderHistoryItem>();

            try
            {
                var order = await _context.Orders.FindAsync(orderId);
                if (order != null)
                {
                    history.Add(new OrderHistoryItem
                    {
                        Status = "待處理",
                        Description = "訂單已建立",
                        Timestamp = order.OrderDate,
                        IsCompleted = true
                    });

                    if (order.PaymentVerified)
                    {
                        history.Add(new OrderHistoryItem
                        {
                            Status = "已付款",
                            Description = "付款確認完成",
                            Timestamp = order.OrderDate.AddMinutes(5),
                            IsCompleted = true
                        });
                    }

                    // 根據當前狀態添加相應的歷史記錄
                    var statusOrder = new[] { "待處理", "處理中", "已發貨", "已送達" };
                    var orderStatus = order.OrderStatus ?? "待處理"; // 修正 Null 問題
                    var currentIndex = Array.IndexOf(statusOrder, orderStatus);

                    for (int i = 1; i <= currentIndex && i < statusOrder.Length; i++)
                    {
                        if (history.Count > i) continue;

                        history.Add(new OrderHistoryItem
                        {
                            Status = statusOrder[i],
                            Description = GetStatusDescription(statusOrder[i]),
                            Timestamp = order.OrderDate.AddDays(i),
                            IsCompleted = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "GetHistoryError", orderId.ToString(), ex.Message);
            }

            return history;
        }

        private static string GetStatusDescription(string? status)
        {
            return status switch
            {
                "待處理" => "訂單已建立，等待處理",
                "處理中" => "訂單處理中，準備出貨",
                "已發貨" => "商品已發貨，正在配送中",
                "已送達" => "商品已送達完成",
                "已取消" => "訂單已取消",
                null => "狀態未知",
                _ => "狀態更新"
            };
        }

        private static string GenerateInvoiceHtml(OrderDetailsDto order)
        {
            return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='UTF-8'>
            <title>訂單收據 #{order.Id}</title>
            <style>
                body {{ font-family: Arial, sans-serif; margin: 20px; }}
                .header {{ text-align: center; margin-bottom: 30px; }}
                .order-info {{ margin-bottom: 20px; }}
                table {{ width: 100%; border-collapse: collapse; }}
                th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
                th {{ background-color: #f2f2f2; }}
                .total {{ font-weight: bold; font-size: 18px; }}
            </style>
        </head>
        <body>
            <div class='header'>
                <h1>Ez購,Ez Life</h1>
                <h2>訂單收據 #{order.Id}</h2>
            </div>
            
            <div class='order-info'>
                <p><strong>訂購日期：</strong>{order.OrderDate:yyyy年MM月dd日 HH:mm}</p>
                <p><strong>客戶姓名：</strong>{order.CustomerName ?? "未提供"}</p>
                <p><strong>收件地址：</strong>{order.ShippingAddress ?? "未提供"}</p>
                <p><strong>付款方式：</strong>{order.PaymentMethod ?? "未指定"}</p>
                <p><strong>訂單狀態：</strong>{order.OrderStatus ?? "未知"}</p>
            </div>

            <table>
                <thead>
                    <tr>
                        <th>商品名稱</th>
                        <th>數量</th>
                        <th>單價</th>
                        <th>小計</th>
                    </tr>
                </thead>
                <tbody>
                    {string.Join("", order.Items.Select(item => $@"
                        <tr>
                            <td>{item.ProductName ?? "未知商品"}</td>
                            <td>{item.Quantity}</td>
                            <td>NT$ {item.UnitPrice:N0}</td>
                            <td>NT$ {item.Subtotal:N0}</td>
                        </tr>"))}
                </tbody>
                <tfoot>
                    <tr class='total'>
                        <td colspan='3'>總計</td>
                        <td>NT$ {order.TotalAmount:N0}</td>
                    </tr>
                </tfoot>
            </table>

            <p style='margin-top: 30px; text-align: center; color: #666;'>
                感謝您的購買！如有任何問題，請聯繫客服。
            </p>
        </body>
        </html>";
        }

        // 輔助類別
        public class OrderHistoryItem
        {
            public string Status { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public DateTime Timestamp { get; set; }
            public bool IsCompleted { get; set; }
        }
    }
}