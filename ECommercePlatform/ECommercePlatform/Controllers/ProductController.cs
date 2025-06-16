using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Services;
using ECommercePlatform.Models.ViewModels;
using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Controllers
{
    /// 統一的產品控制器（整合前台和後台功能）
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly OperationLogService _logService;

        public ProductController(
            ApplicationDbContext context,
            IInventoryService inventoryService,
            OperationLogService logService)
        {
            _context = context;
            _inventoryService = inventoryService;
            _logService = logService;
        }
        #region API 方法

        //API: 獲取產品列表
        [HttpGet("/api/products/all")]
        public async Task<IActionResult> GetProductsApi()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.Id)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    CurrentPrice = p.CurrentPrice,
                    p.ImageUrl,
                    HasDiscount = p.HasDiscount,
                    AverageRating = p.AverageRating
                })
                .ToListAsync();

            return Json(new { success = true, data = products });
        }

        //API: 根據ID獲取產品
        [HttpGet("/api/products/{id}")]
        public async Task<IActionResult> GetProductApi(int id)
        {
            var product = await _context.Products
                .Where(p => p.Id == id && p.IsActive)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Price,
                    CurrentPrice = p.CurrentPrice,
                    p.ImageUrl,
                    HasDiscount = p.HasDiscount,
                    AverageRating = p.AverageRating
                })
                .FirstOrDefaultAsync();

            if (product == null) return NotFound();
            return Json(new { success = true, data = product });
        }

        #endregion

        /// 前台：商品列表頁面
        [HttpGet]
        public async Task<IActionResult> Index(string? sort = "latest", string? keyword = "",
            decimal? minPrice = null, decimal? maxPrice = null, bool? onSale = null, int page = 1)
        {
            const int pageSize = 12;

            var query = _context.Products
                .Where(p => p.IsActive)
                .AsQueryable();

            // 關鍵字搜尋
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(p => p.Name.Contains(keyword) ||
                                   (p.Description != null && p.Description.Contains(keyword)));
            }

            // 價格範圍篩選
            if (minPrice.HasValue)
            {
                query = query.Where(p => p.Price >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                query = query.Where(p => p.Price <= maxPrice.Value);
            }

            // 特價商品篩選
            if (onSale.HasValue && onSale.Value)
            {
                var now = DateTime.Now;
                query = query.Where(p => p.DiscountPrice.HasValue &&
                                   p.DiscountStart.HasValue && p.DiscountEnd.HasValue &&
                                   now >= p.DiscountStart.Value && now <= p.DiscountEnd.Value);
            }

            // 排序
            query = sort switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "name" => query.OrderBy(p => p.Name),
                "latest" => query.OrderByDescending(p => p.CreatedAt),
                "popular" => query.OrderByDescending(p => p.OrderItems.Sum(oi => oi.Quantity)),
                _ => query.OrderByDescending(p => p.CreatedAt)
            };

            var totalItems = await query.CountAsync();
            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(p => p.Reviews.Where(r => r.IsVisible))
                .ToListAsync();

            var result = new PagedResult<Product>
            {
                Items = products,
                TotalItems = totalItems,
                PageNumber = page,
                PageSize = pageSize
            };

            // 保存篩選參數到 ViewBag
            ViewBag.Sort = sort;
            ViewBag.Keyword = keyword;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.OnSale = onSale;

            return View(result);
        }

        /// 前台：商品詳情頁面
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var product = await _context.Products
                .Include(p => p.Reviews.Where(r => r.IsVisible))
                .ThenInclude(r => r.User)
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (product == null)
            {
                return NotFound();
            }

            // 獲取庫存狀態
            var availability = await _inventoryService.CheckAvailabilityAsync(id, 1);
            ViewBag.StockStatus = availability;

            // 獲取推薦商品（同類或相關）
            var relatedProducts = await _context.Products
                .Where(p => p.IsActive && p.Id != id)
                .OrderBy(p => Guid.NewGuid()) // 隨機排序
                .Take(4)
                .ToListAsync();
            ViewBag.RelatedProducts = relatedProducts;

            // 記錄商品瀏覽
            _logService.Log("Product", "View", id.ToString(), $"瀏覽商品：{product.Name}");

            return View(product);
        }

        /// 後台：商品管理列表（需要管理員權限）
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("admin/products")]
        public async Task<IActionResult> AdminIndex(string? keyword = "", bool? isActive = null, int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Products.AsQueryable();

            // 關鍵字搜尋
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(p => p.Name.Contains(keyword) ||
                                   (p.Description != null && p.Description.Contains(keyword)));
            }

            // 狀態篩選
            if (isActive.HasValue)
            {
                query = query.Where(p => p.IsActive == isActive.Value);
            }

            var totalItems = await query.CountAsync();
            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new PagedResult<Product>
            {
                Items = products,
                TotalItems = totalItems,
                PageNumber = page,
                PageSize = pageSize
            };

            ViewBag.Keyword = keyword;
            ViewBag.IsActive = isActive;

            return View("Admin/Index", result);
        }

        /// 後台：新增商品頁面
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("admin/products/create")]
        public IActionResult AdminCreate()
        {
            return View("Admin/Create", new ProductUploadDto());
        }

        /// 後台：處理新增商品
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("admin/products/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminCreate(ProductUploadDto dto)
        {
            if (!ModelState.IsValid)
            {
                return View("Admin/Create", dto);
            }

            try
            {
                var product = new Product
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    Price = dto.Price,
                    DiscountPrice = dto.DiscountPrice,
                    DiscountStart = dto.DiscountStart,
                    DiscountEnd = dto.DiscountEnd,
                    Stock = dto.Stock ?? 0,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                // 處理圖片上傳
                if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    var imageUrl = await SaveProductImageAsync(dto.ImageFile);
                    if (imageUrl != null)
                    {
                        product.ImageUrl = imageUrl;
                    }
                }
                else if (!string.IsNullOrEmpty(dto.ImageUrl))
                {
                    product.ImageUrl = dto.ImageUrl;
                }

                _context.Products.Add(product);
                await _context.SaveChangesAsync();

                // 記錄庫存初始化
                if (product.Stock > 0)
                {
                    await _inventoryService.AdjustStockAsync(
                        product.Id, product.Stock, "新商品初始庫存",
                        int.Parse(User.Claims.First(c => c.Type == "UserId").Value));
                }

                _logService.Log("Product", "Create", product.Id.ToString(),
                    $"新增商品：{product.Name}");

                TempData["Success"] = "商品新增成功！";
                return RedirectToAction("AdminIndex");
            }
            catch (Exception ex)
            {
                _logService.Log("Product", "CreateError", "", ex.Message);
                ModelState.AddModelError("", "新增商品失敗，請稍後再試");
                return View("Admin/Create", dto);
            }
        }

        /// 後台：編輯商品頁面
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("admin/products/edit/{id}")]
        public async Task<IActionResult> AdminEdit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var dto = new ProductUploadDto
            {
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                DiscountPrice = product.DiscountPrice,
                DiscountStart = product.DiscountStart,
                DiscountEnd = product.DiscountEnd,
                ImageUrl = product.ImageUrl,
                Stock = product.Stock
            };

            ViewBag.Product = product;
            return View("Admin/Edit", dto);
        }

        /// 後台：處理編輯商品
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("admin/products/edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminEdit(int id, ProductUploadDto dto)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Product = product;
                return View("Admin/Edit", dto);
            }

            try
            {
                var oldStock = product.Stock;

                // 更新基本資訊
                product.Name = dto.Name;
                product.Description = dto.Description;
                product.Price = dto.Price;
                product.DiscountPrice = dto.DiscountPrice;
                product.DiscountStart = dto.DiscountStart;
                product.DiscountEnd = dto.DiscountEnd;

                // 處理庫存變更
                if (dto.Stock.HasValue && dto.Stock.Value != oldStock)
                {
                    var stockDifference = dto.Stock.Value - oldStock;
                    product.Stock = dto.Stock.Value;

                    // 記錄庫存調整
                    await _inventoryService.AdjustStockAsync(
                        product.Id, stockDifference, "管理員調整庫存",
                        int.Parse(User.Claims.First(c => c.Type == "UserId").Value));
                }

                // 處理圖片
                if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    var imageUrl = await SaveProductImageAsync(dto.ImageFile);
                    if (imageUrl != null)
                    {
                        product.ImageUrl = imageUrl;
                    }
                }
                else if (!string.IsNullOrEmpty(dto.ImageUrl))
                {
                    product.ImageUrl = dto.ImageUrl;
                }

                await _context.SaveChangesAsync();

                _logService.Log("Product", "Update", id.ToString(),
                    $"更新商品：{product.Name}");

                TempData["Success"] = "商品更新成功！";
                return RedirectToAction("AdminIndex");
            }
            catch (Exception ex)
            {
                _logService.Log("Product", "UpdateError", id.ToString(), ex.Message);
                ModelState.AddModelError("", "更新商品失敗，請稍後再試");
                ViewBag.Product = product;
                return View("Admin/Edit", dto);
            }
        }

        //後台：刪除/停用商品
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("admin/products/delete/{id}")]
        public async Task<IActionResult> AdminDelete(int id)
        {
            try
            {
                var product = await _context.Products.FindAsync(id);
                if (product == null)
                {
                    return Json(new { success = false, message = "商品不存在" });
                }

                // 檢查是否有相關訂單
                var hasOrders = await _context.OrderItems.AnyAsync(oi => oi.ProductId == id);

                if (hasOrders)
                {
                    // 有訂單記錄，只停用不刪除
                    product.IsActive = false;
                    await _context.SaveChangesAsync();

                    _logService.Log("Product", "Deactivate", id.ToString(),
                        $"停用商品：{product.Name}");

                    return Json(new { success = true, message = "商品已停用" });
                }
                else
                {
                    // 沒有訂單記錄，可以完全刪除
                    _context.Products.Remove(product);
                    await _context.SaveChangesAsync();

                    _logService.Log("Product", "Delete", id.ToString(),
                        $"刪除商品：{product.Name}");

                    return Json(new { success = true, message = "商品已刪除" });
                }
            }
            catch (Exception ex)
            {
                _logService.Log("Product", "DeleteError", id.ToString(), ex.Message);
                return Json(new { success = false, message = "操作失敗，請稍後再試" });
            }
        }

        //後台：庫存管理頁面
        [HttpGet]
        [Authorize(Roles = "Admin")]
        [Route("admin/products/inventory")]
        public async Task<IActionResult> InventoryManagement()
        {
            var lowStockProducts = await _inventoryService.GetLowStockProductsAsync();
            return View("Admin/Inventory", lowStockProducts);
        }

        // 後台：調整庫存
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [Route("admin/products/adjust-stock")]
        public async Task<IActionResult> AdjustStock(int productId, int adjustment, string reason)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
                var success = await _inventoryService.AdjustStockAsync(productId, adjustment, reason, userId);

                if (success)
                {
                    return Json(new { success = true, message = "庫存調整成功" });
                }
                else
                {
                    return Json(new { success = false, message = "庫存調整失敗" });
                }
            }
            catch (Exception ex)
            {
                _logService.Log("Product", "AdjustStockError", productId.ToString(), ex.Message);
                return Json(new { success = false, message = "操作失敗，請稍後再試" });
            }
        }

        //API：獲取商品列表（給前端 AJAX 使用）
        [HttpGet]
        [Route("api/products")]
        public async Task<IActionResult> GetProducts(string? keyword = "", int page = 1, int pageSize = 12)
        {
            try
            {
                var query = _context.Products
                    .Where(p => p.IsActive)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(keyword))
                {
                    query = query.Where(p => p.Name.Contains(keyword) ||
                                       (p.Description != null && p.Description.Contains(keyword)));
                }

                var totalItems = await query.CountAsync();
                var products = await query
                    .OrderByDescending(p => p.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(p => new
                    {
                        p.Id,
                        p.Name,
                        p.Description,
                        p.Price,
                        CurrentPrice = p.CurrentPrice,
                        p.ImageUrl,
                        p.Stock,
                        HasDiscount = p.HasDiscount,
                        AverageRating = p.AverageRating
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = products,
                    totalItems = totalItems,
                    page = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalItems / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logService.Log("Product", "GetProductsError", "", ex.Message);
                return Json(new { success = false, message = "獲取商品列表失敗" });
            }
        }

        //保存商品圖片
        private async Task<string?> SaveProductImageAsync(IFormFile imageFile)
        {
            try
            {
                var ext = Path.GetExtension(imageFile.FileName);
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                if (!allowedExtensions.Contains(ext.ToLower()))
                {
                    return null;
                }

                var fileName = $"{Guid.NewGuid():N}{ext}";
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");

                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var filePath = Path.Combine(uploadsFolder, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);

                return $"/uploads/products/{fileName}";
            }
            catch (Exception ex)
            {
                _logService.Log("Product", "SaveImageError", "", ex.Message);
                return null;
            }
        }
    }
}