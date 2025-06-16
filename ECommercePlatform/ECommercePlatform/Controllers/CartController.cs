using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Models.ViewModels;
using ECommercePlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System;
using System.Security.Claims;

namespace ECommercePlatform.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly OperationLogService _logService;

        public CartController(
            ApplicationDbContext context,
            IInventoryService inventoryService,
            OperationLogService logService)
        {
            _context = context;
            _inventoryService = inventoryService;
            _logService = logService;
        }

        /// 購物車頁面
        [HttpGet]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> Index()
        {
            try{
                var userId = GetCurrentUserId();
                if (User.Identity?.IsAuthenticated == false)
                {
                    return RedirectToAction("Login", "Account");
                }

                var cartItems = await _context.CartItems
                    .Include(c => c.Product)
                    .Include(c => c.User)
                    .Where(c => c.UserId == userId)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();

                // 檢查每個商品的庫存狀態
                var cartItemsWithStatus = new List<CartItemWithStatus>();
                var userCartItems = new List<CartItem>();

                foreach (var item in cartItems)
                {
                    var availability = await _inventoryService.CheckAvailabilityAsync(
                        item.ProductId, item.Quantity);

                    cartItemsWithStatus.Add(new CartItemWithStatus
                    {
                        CartItem = item,
                        IsAvailable = availability.IsAvailable,
                        AvailableQuantity = availability.AvailableQuantity,
                        ErrorMessage = availability.ErrorMessage
                    });
                    if (item.Quantity > 0)
                    {
                        var product = _context.Products
                            .Include(p => p.CartItems)
                            .Where(p => item.ProductId == p.Id)
                            .FirstOrDefault();

                        userCartItems.Add(new CartItem
                        {
                            Id = item.Id,
                            UserId = item.UserId,
                            ProductId = item.ProductId,
                            Quantity = item.Quantity,
                            CreatedAt = item.CreatedAt,
                            UpdatedAt = item.UpdatedAt,
                            Product = product
                        });
                    }
                }
                var result = new CartItemListViewModel
                {
                    CartItem = userCartItems,
                    CartItemWithStatus = cartItemsWithStatus
                };

                    return View(result);
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "IndexError", "", ex.Message);
                return View(new CartItemListViewModel());
            }
        }


        /// 加入商品到購物車（整合庫存檢查）
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Json(new { success = false, message = "請先登入" });
                }

                // 驗證請求數據
                if (request.ProductId <= 0 || request.Quantity <= 0)
                {
                    return Json(new { success = false, message = "請求參數無效" });
                }

                // 檢查商品是否存在且有效
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    return Json(new { success = false, message = "商品不存在" });
                }

                if (!product.IsActive)
                {
                    return Json(new { success = false, message = "商品已下架" });
                }

                // 檢查當前購物車中的數量
                var existingCartItem = await _context.CartItems
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId);

                var currentCartQuantity = existingCartItem?.Quantity ?? 0;
                var totalRequestedQuantity = currentCartQuantity + request.Quantity;

                // 使用庫存服務檢查可用性
                var availability = await _inventoryService.CheckAvailabilityAsync(
                    request.ProductId, totalRequestedQuantity);

                if (!availability.IsAvailable)
                {
                    return Json(new
                    {
                        success = false,
                        message = availability.ErrorMessage,
                        availableQuantity = availability.AvailableQuantity,
                        currentInCart = currentCartQuantity
                    });
                }

                // 更新或新增購物車項目
                if (existingCartItem != null)
                {
                    existingCartItem.Quantity += request.Quantity;
                    existingCartItem.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    var cartItem = new CartItem
                    {
                        UserId = userId,
                        ProductId = request.ProductId,
                        Quantity = request.Quantity,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.CartItems.Add(cartItem);
                }

                await _context.SaveChangesAsync();

                // 記錄操作日誌
                _logService.Log("Cart", "AddItem", request.ProductId.ToString(),
                    $"加入購物車：{product.Name} x{request.Quantity}");

                return Json(new
                {
                    success = true,
                    message = "成功加入購物車",
                    productName = product.Name,
                    quantity = request.Quantity
                });
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "AddItemError", request.ProductId.ToString(), ex.Message);
                return Json(new { success = false, message = "系統錯誤，請稍後再試" });
            }
        }

        //獲取購物車商品數量
        [HttpGet]
        public async Task<IActionResult> GetCartCount()
        {
            try
            {
                if (!User.Identity?.IsAuthenticated == true)
                {
                    return Json(new { count = 0 });
                }

                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Json(new { count = 0 });
                }

                var count = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .SumAsync(c => c.Quantity);

                return Json(new { count = count });
            }
            catch
            {
                return Json(new { count = 0 });
            }
        }

        // 更新購物車商品數量（整合庫存檢查）
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> UpdateQuantity(int cartItemId, int quantity)
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartItem = await _context.CartItems
                    .Include(c => c.Product)
                    .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

                if (cartItem == null)
                {
                    return Json(new { success = false, message = "購物車項目不存在" });
                }

                if (quantity <= 0)
                {
                    // 刪除項目
                    _context.CartItems.Remove(cartItem);
                    await _context.SaveChangesAsync();

                    _logService.Log("Cart", "RemoveItem", cartItem.ProductId.ToString(),
                        $"從購物車移除：{cartItem.Product.Name}");

                    return Json(new { success = true, message = "已移除商品" });
                }

                // 檢查庫存可用性
                var availability = await _inventoryService.CheckAvailabilityAsync(
                    cartItem.ProductId, quantity);

                if (!availability.IsAvailable)
                {
                    return Json(new
                    {
                        success = false,
                        message = availability.ErrorMessage,
                        availableQuantity = availability.AvailableQuantity
                    });
                }

                // 更新數量
                var oldQuantity = cartItem.Quantity;
                cartItem.Quantity = quantity;
                cartItem.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logService.Log("Cart", "UpdateQuantity", cartItem.ProductId.ToString(),
                    $"更新購物車數量：{cartItem.Product.Name} {oldQuantity}→{quantity}");

                return Json(new { success = true, message = "數量更新成功" });
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "UpdateQuantityError", cartItemId.ToString(), ex.Message);
                return Json(new { success = false, message = "更新失敗，請稍後再試" });
            }
        }

        //移除購物車商品
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> RemoveItem(int cartItemId)
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartItem = await _context.CartItems
                    .Include(c => c.Product)
                    .FirstOrDefaultAsync(c => c.Id == cartItemId && c.UserId == userId);

                if (cartItem != null)
                {
                    var productName = cartItem.Product.Name;
                    _context.CartItems.Remove(cartItem);
                    await _context.SaveChangesAsync();

                    _logService.Log("Cart", "RemoveItem", cartItem.ProductId.ToString(),
                        $"從購物車移除：{productName}");
                }

                return Json(new { success = true, message = "商品已移除" });
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "RemoveItemError", cartItemId.ToString(), ex.Message);
                return Json(new { success = false, message = "移除失敗，請稍後再試" });
            }
        }

        //清空購物車
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> ClearCart()
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartItems = await _context.CartItems
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                if (cartItems.Any())
                {
                    _context.CartItems.RemoveRange(cartItems);
                    await _context.SaveChangesAsync();

                    _logService.Log("Cart", "Clear", "",
                        $"清空購物車，共 {cartItems.Count} 項商品");
                }

                return Json(new { success = true, message = "購物車已清空" });
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "ClearError", "", ex.Message);
                return Json(new { success = false, message = "清空失敗，請稍後再試" });
            }
        }

        // 批量檢查購物車商品庫存狀態
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> CheckCartStock()
        {
            try
            {
                var userId = GetCurrentUserId();
                var cartItems = await _context.CartItems
                    .Include(c => c.Product)
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                var stockStatus = new List<object>();

                foreach (var item in cartItems)
                {
                    var availability = await _inventoryService.CheckAvailabilityAsync(
                        item.ProductId, item.Quantity);

                    stockStatus.Add(new
                    {
                        cartItemId = item.Id,
                        productId = item.ProductId,
                        productName = item.Product.Name,
                        requestedQuantity = item.Quantity,
                        isAvailable = availability.IsAvailable,
                        availableQuantity = availability.AvailableQuantity,
                        errorMessage = availability.ErrorMessage
                    });
                }

                return Json(new { success = true, stockStatus = stockStatus });
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "CheckStockError", "", ex.Message);
                return Json(new { success = false, message = "庫存檢查失敗" });
            }
        }

        //獲取當前用戶ID
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue("UserId");
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        //檢查是否為管理員
        private bool IsAdmin()
        {
            var userRole = User.FindFirstValue("UserRole");
            return userRole == "Admin" || userRole == "Engineer";
        }
    }

    // DTO 和輔助類別
    //加入購物車請求
    public class AddToCartRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    //帶庫存狀態的購物車項目
    public class CartItemWithStatus
    {
        public CartItem CartItem { get; set; } = null!;
        public bool IsAvailable { get; set; }
        public int AvailableQuantity { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        // 便利屬性
        public bool HasStockIssue => !IsAvailable;
        public bool IsPartiallyAvailable => AvailableQuantity > 0 && AvailableQuantity < CartItem.Quantity;
        public int ExcessQuantity => Math.Max(0, CartItem.Quantity - AvailableQuantity);
    }
}