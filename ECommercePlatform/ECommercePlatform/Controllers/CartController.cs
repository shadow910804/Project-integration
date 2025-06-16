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

        /// �ʪ�������
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

                // �ˬd�C�Ӱӫ~���w�s���A
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


        /// �[�J�ӫ~���ʪ����]��X�w�s�ˬd�^
        [HttpPost]
        [Authorize(Roles = "User")]
        public async Task<IActionResult> AddToCart([FromBody] AddToCartRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == 0)
                {
                    return Json(new { success = false, message = "�Х��n�J" });
                }

                // ���ҽШD�ƾ�
                if (request.ProductId <= 0 || request.Quantity <= 0)
                {
                    return Json(new { success = false, message = "�ШD�ѼƵL��" });
                }

                // �ˬd�ӫ~�O�_�s�b�B����
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null)
                {
                    return Json(new { success = false, message = "�ӫ~���s�b" });
                }

                if (!product.IsActive)
                {
                    return Json(new { success = false, message = "�ӫ~�w�U�[" });
                }

                // �ˬd��e�ʪ��������ƶq
                var existingCartItem = await _context.CartItems
                    .FirstOrDefaultAsync(c => c.UserId == userId && c.ProductId == request.ProductId);

                var currentCartQuantity = existingCartItem?.Quantity ?? 0;
                var totalRequestedQuantity = currentCartQuantity + request.Quantity;

                // �ϥήw�s�A���ˬd�i�Ω�
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

                // ��s�ηs�W�ʪ�������
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

                // �O���ާ@��x
                _logService.Log("Cart", "AddItem", request.ProductId.ToString(),
                    $"�[�J�ʪ����G{product.Name} x{request.Quantity}");

                return Json(new
                {
                    success = true,
                    message = "���\�[�J�ʪ���",
                    productName = product.Name,
                    quantity = request.Quantity
                });
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "AddItemError", request.ProductId.ToString(), ex.Message);
                return Json(new { success = false, message = "�t�ο��~�A�еy��A��" });
            }
        }

        //����ʪ����ӫ~�ƶq
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

        // ��s�ʪ����ӫ~�ƶq�]��X�w�s�ˬd�^
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
                    return Json(new { success = false, message = "�ʪ������ؤ��s�b" });
                }

                if (quantity <= 0)
                {
                    // �R������
                    _context.CartItems.Remove(cartItem);
                    await _context.SaveChangesAsync();

                    _logService.Log("Cart", "RemoveItem", cartItem.ProductId.ToString(),
                        $"�q�ʪ��������G{cartItem.Product.Name}");

                    return Json(new { success = true, message = "�w�����ӫ~" });
                }

                // �ˬd�w�s�i�Ω�
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

                // ��s�ƶq
                var oldQuantity = cartItem.Quantity;
                cartItem.Quantity = quantity;
                cartItem.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logService.Log("Cart", "UpdateQuantity", cartItem.ProductId.ToString(),
                    $"��s�ʪ����ƶq�G{cartItem.Product.Name} {oldQuantity}��{quantity}");

                return Json(new { success = true, message = "�ƶq��s���\" });
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "UpdateQuantityError", cartItemId.ToString(), ex.Message);
                return Json(new { success = false, message = "��s���ѡA�еy��A��" });
            }
        }

        //�����ʪ����ӫ~
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
                        $"�q�ʪ��������G{productName}");
                }

                return Json(new { success = true, message = "�ӫ~�w����" });
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "RemoveItemError", cartItemId.ToString(), ex.Message);
                return Json(new { success = false, message = "�������ѡA�еy��A��" });
            }
        }

        //�M���ʪ���
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
                        $"�M���ʪ����A�@ {cartItems.Count} ���ӫ~");
                }

                return Json(new { success = true, message = "�ʪ����w�M��" });
            }
            catch (Exception ex)
            {
                _logService.Log("Cart", "ClearError", "", ex.Message);
                return Json(new { success = false, message = "�M�ť��ѡA�еy��A��" });
            }
        }

        // ��q�ˬd�ʪ����ӫ~�w�s���A
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
                return Json(new { success = false, message = "�w�s�ˬd����" });
            }
        }

        //�����e�Τ�ID
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue("UserId");
            return int.TryParse(userIdClaim, out int userId) ? userId : 0;
        }

        //�ˬd�O�_���޲z��
        private bool IsAdmin()
        {
            var userRole = User.FindFirstValue("UserRole");
            return userRole == "Admin" || userRole == "Engineer";
        }
    }

    // DTO �M���U���O
    //�[�J�ʪ����ШD
    public class AddToCartRequest
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    //�a�w�s���A���ʪ�������
    public class CartItemWithStatus
    {
        public CartItem CartItem { get; set; } = null!;
        public bool IsAvailable { get; set; }
        public int AvailableQuantity { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        // �K�Q�ݩ�
        public bool HasStockIssue => !IsAvailable;
        public bool IsPartiallyAvailable => AvailableQuantity > 0 && AvailableQuantity < CartItem.Quantity;
        public int ExcessQuantity => Math.Max(0, CartItem.Quantity - AvailableQuantity);
    }
}