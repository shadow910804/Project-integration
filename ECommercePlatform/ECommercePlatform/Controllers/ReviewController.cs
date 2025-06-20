using Azure;
using Azure.Core;
using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Models.DTOs;
using ECommercePlatform.Models.ViewModels;
using ECommercePlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using static ECommercePlatform.Models.DTOs.ReviewDTOs;

namespace ECommercePlatform.Controllers
{
    public class ReviewController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly OperationLogService _log;

        public ReviewController(ApplicationDbContext context, EmailService emailService, OperationLogService log)
        {
            _context = context;
            _emailService = emailService;
            _log = log;
        }

        // 評價列表頁面（改善分頁和篩選）
        [HttpGet]
        public async Task<IActionResult> Index(int? productId = null, string sort = "latest",
            string keyword = "", int? scoreFilter = null, int page = 1, int pageSize = 12)
        {
            try
            {
                if (TempData["ReviewResult"]!=null)
                {
                    ViewBag.ReviewResult = TempData["ReviewResult"];
                }
                var query = _context.Reviews
                    .Include(r => r.User)
                    .Include(r => r.Product)
                    .Where(r => r.IsVisible)
                    .AsQueryable();

                // 產品篩選
                if (productId.HasValue)
                {
                    query = query.Where(r => r.ProductId == productId.Value);
                    var product = await _context.Products.FindAsync(productId.Value);
                    ViewBag.ProductName = product?.Name;
                    ViewBag.ProductId = productId;
                }

                // 關鍵字搜尋
                if (!string.IsNullOrEmpty(keyword))
                {
                    query = query.Where(r => r.Content.Contains(keyword) ||
                                       r.Product.Name.Contains(keyword) ||
                                       (r.UserName != null && r.UserName.Contains(keyword)));
                }

                // 評分篩選
                if (scoreFilter.HasValue)
                {
                    query = query.Where(r => r.Rating == scoreFilter.Value);
                }

                // 排序
                query = sort switch
                {
                    "latest" => query.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.CreatedAt),
                    "oldest" => query.OrderByDescending(r => r.IsPinned).ThenBy(r => r.CreatedAt),
                    "highscore" => query.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt),
                    "lowscore" => query.OrderByDescending(r => r.IsPinned).ThenBy(r => r.Rating).ThenByDescending(r => r.CreatedAt),
                    "helpful" => query.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.HelpfulCount).ThenByDescending(r => r.CreatedAt),
                    _ => query.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.CreatedAt)
                };

                var totalItems = await query.CountAsync();
                var reviews = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // 計算統計數據
                var allReviews = await _context.Reviews
                    .Where(r => r.IsVisible && (!productId.HasValue || r.ProductId == productId.Value))
                    .ToListAsync();

                var result = new ReviewListViewModel
                {
                    Reviews = reviews,
                    TotalItems = totalItems,
                    PageNumber = page,
                    PageSize = pageSize,
                    TotalReviews = allReviews.Count,
                    AverageScore = allReviews.Any() ? allReviews.Average(r => r.Rating) : 0,
                    RatingDistribution = allReviews
                        .GroupBy(r => r.Rating)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    // 新增統計
                    RecentReviewsCount = allReviews.Count(r => r.CreatedAt >= DateTime.UtcNow.AddDays(-7)),
                    WithImagesCount = allReviews.Count(r => r.HasImage),
                    WithAdminReplyCount = allReviews.Count(r => r.HasAdminReply)
                };

                // 保存篩選參數
                ViewBag.ProductId = productId;
                ViewBag.Sort = sort;
                ViewBag.Keyword = keyword;
                ViewBag.ScoreFilter = scoreFilter;
                ViewBag.PageSize = pageSize;

                // 獲取可評價的商品列表（已購買但未評價）
                if (User.Identity?.IsAuthenticated == true)
                {
                    var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
                    ViewBag.PurchasedProducts = await GetPurchasedProductsForReview(userId);
                    ViewBag.userId=userId;
                }

                // 支援 AJAX 請求
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    return PartialView("_ReviewListPartial", result);
                }

                return View(result);
            }
            catch (Exception ex)
            {
                _log.Log("Review", "IndexError", "", ex.Message);
                return View(new ReviewListViewModel());
            }
        }

        // 新增評價（改善驗證和錯誤處理）
        [HttpPost]
        [Authorize(Roles = "User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromForm] CreateReviewRequest request)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
                var userName = User.Identity?.Name ?? "";

                // 檢查商品是否存在
                var product = await _context.Products.FindAsync(request.ProductId);
                if (product == null || !product.IsActive)
                {
                    return Json(new { success = false, message = "商品不存在或已下架" });
                }

                // 檢查是否已經評價過
                var existingReview = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.UserId == userId && r.ProductId == request.ProductId);

                if (existingReview != null)
                {
                    return Json(new { success = false, message = "您已經評價過此商品，每個商品只能評價一次" });
                }

                // 檢查是否有購買記錄
                var hasPurchased = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .AnyAsync(oi => oi.ProductId == request.ProductId &&
                                  oi.Order.UserId == userId &&
                                  oi.Order.OrderStatus == "已送達");

                if (!hasPurchased)
                {
                    return Json(new { success = false, message = "只有購買過的商品才能評價" });
                }

                // 處理圖片上傳（改善驗證）
                byte[]? imageData = null;
                string? imageFileName = null;
                string? imageContentType = null;

                if (request.ImageFile != null && request.ImageFile.Length > 0)
                {
                    // 檔案大小限制（5MB）
                    if (request.ImageFile.Length > 5 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "圖片檔案不能超過 5MB" });
                    }

                    // 檔案類型驗證
                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                    if (!allowedTypes.Contains(request.ImageFile.ContentType.ToLower()))
                    {
                        return Json(new { success = false, message = "只允許上傳 JPG、PNG、GIF、WebP 格式的圖片" });
                    }

                    // 讀取圖片數據
                    using var ms = new MemoryStream();
                    await request.ImageFile.CopyToAsync(ms);
                    imageData = ms.ToArray();
                    imageFileName = request.ImageFile.FileName;
                    imageContentType = request.ImageFile.ContentType;
                }

                // 創建評價
                var review = new Review
                {
                    UserId = userId,
                    ProductId = request.ProductId,
                    UserName = userName,
                    Content = request.Content.Trim(),
                    Rating = request.Rating,
                    ImageData = imageData,
                    ImageFileName = imageFileName,
                    ImageContentType = imageContentType,
                    CreatedAt = DateTime.UtcNow,
                    IsVisible = true
                };

                _context.Reviews.Add(review);
                await _context.SaveChangesAsync();

                _log.Log("Review", "Create", review.Id.ToString(),
                    $"新增評價：{request.Rating}星，商品: {product.Name}");

                // 發送評價通知給管理員（可選）
                try
                {
                    await _emailService.SendComplainMailAsync(
                        "新商品評價通知",
                        $"用戶 {userName} 對商品 {product.Name} 給予 {request.Rating} 星評價");
                }
                catch (Exception ex)
                {
                    _log.Log("Review", "EmailError", review.Id.ToString(), ex.Message);
                }

                return Json(new
                {
                    success = true,
                    message = "評價新增成功！感謝您的回饋",
                    reviewId = review.Id,
                    rating = review.Rating,
                    userName = review.DisplayUserName
                });
            }
            catch (Exception ex)
            {
                _log.Log("Review", "CreateError", "", ex.Message);
                return Json(new { success = false, message = "新增評價失敗，請稍後重試" });
            }
        }

        // 更新評價
        [HttpPost]
        [Authorize(Roles = "User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update([FromForm] UpdateReviewRequest request)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
                var review = await _context.Reviews
                    .FirstOrDefaultAsync(r => r.Id == request.ReviewId && r.UserId == userId);

                if (review == null)
                {
                    return Json(new { success = false, message = "評價不存在或無權限修改" });
                }

                // 處理圖片上傳（改善驗證）
                byte[]? imageData = null;
                string? imageFileName = null;
                string? imageContentType = null;

                if (request.ImageFile != null && request.ImageFile.Length > 0)
                {
                    // 檔案大小限制（5MB）
                    if (request.ImageFile.Length > 5 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "圖片檔案不能超過 5MB" });
                    }

                    // 檔案類型驗證
                    var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
                    if (!allowedTypes.Contains(request.ImageFile.ContentType.ToLower()))
                    {
                        return Json(new { success = false, message = "只允許上傳 JPG、PNG、GIF、WebP 格式的圖片" });
                    }

                    // 讀取圖片數據
                    using var ms = new MemoryStream();
                    await request.ImageFile.CopyToAsync(ms);
                    imageData = ms.ToArray();
                    imageFileName = request.ImageFile.FileName;
                    imageContentType = request.ImageFile.ContentType;
                }

                review.Content = request.Content.Trim();
                review.Rating = request.Rating;
                review.UpdatedAt = DateTime.UtcNow;
                review.ImageData = imageData;
                review.ImageFileName = imageFileName;
                review.ImageContentType = imageContentType;

                await _context.SaveChangesAsync();

                _log.Log("Review", "Update", request.ReviewId.ToString(), "更新評價");

                return Json(new { success = true, message = "評價更新成功" });
            }
            catch (Exception ex)
            {
                _log.Log("Review", "UpdateError", request.ReviewId.ToString(), ex.Message);
                return Json(new { success = false, message = "更新評價失敗" });
            }
        }

        // 刪除評價
        [HttpPost]
        [Authorize(Roles = "User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int reviewId)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
                var userRole = User.Claims.FirstOrDefault(c => c.Type == "UserRole")?.Value ?? "";

                var review = await _context.Reviews.FindAsync(reviewId);
                if (review == null)
                {
                    TempData["ReviewResult"] = "評價不存在";
                    return RedirectToAction("Index", "Review");
                }

                // 檢查權限：評價者本人或管理員
                if (review.UserId != userId && userRole != "Admin" && userRole != "Engineer")
                {
                    TempData["ReviewResult"] = "無權限刪除此評價";
                    return RedirectToAction("Index", "Review");
                }

                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();

                _log.Log("Review", "Delete", reviewId.ToString(), "刪除評價");

                TempData["ReviewResult"] = "評價成功刪除";
                return RedirectToAction("Index", "Review");
            }
            catch (Exception ex)
            {
                _log.Log("Review", "DeleteError", reviewId.ToString(), ex.Message);
                TempData["ReviewResult"] = "刪除評價失敗";
                return RedirectToAction("Index", "Review");
            }
        }

        // 評價有用性投票
        [HttpPost]
        [Authorize(Roles = "User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoteHelpful(int reviewId, bool isHelpful)
        {
            try
            {
                var review = await _context.Reviews.FindAsync(reviewId);
                if (review == null)
                {
                    return Json(new { success = false, message = "評價不存在" });
                }

                if (isHelpful)
                {
                    review.HelpfulCount++;
                }
                else
                {
                    review.UnhelpfulCount++;
                }

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    helpfulCount = review.HelpfulCount,
                    unhelpfulCount = review.UnhelpfulCount,
                    helpfulPercentage = review.HelpfulPercentage
                });
            }
            catch (Exception ex)
            {
                _log.Log("Review", "VoteError", reviewId.ToString(), ex.Message);
                return Json(new { success = false, message = "投票失敗" });
            }
        }

        // 管理員回覆評價
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminReply(int reviewId, string reply)
        {
            try
            {
                var review = await _context.Reviews.FindAsync(reviewId);
                if (review == null)
                {
                    return Json(new { success = false, message = "評價不存在" });
                }

                if (string.IsNullOrWhiteSpace(reply) || reply.Length > 500)
                {
                    return Json(new { success = false, message = "回覆內容長度必須在1-500字之間" });
                }

                var adminName = User.Identity?.Name ?? "管理員";

                review.AdminReply = reply.Trim();
                review.AdminReplyTime = DateTime.UtcNow;
                review.AdminRepliedBy = adminName;

                await _context.SaveChangesAsync();

                _log.Log("Review", "AdminReply", reviewId.ToString(), $"管理員回覆評價: {adminName}");

                return Json(new
                {
                    success = true,
                    message = "回覆成功",
                    reply = review.AdminReply,
                    replyTime = review.AdminReplyTime?.ToString("yyyy-MM-dd HH:mm"),
                    repliedBy = review.AdminRepliedBy
                });
            }
            catch (Exception ex)
            {
                _log.Log("Review", "AdminReplyError", reviewId.ToString(), ex.Message);
                return Json(new { success = false, message = "回覆失敗" });
            }
        }

        // 置頂/取消置頂評價
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePin(int reviewId)
        {
            try
            {
                var review = await _context.Reviews.FindAsync(reviewId);
                if (review == null)
                {
                    return Json(new { success = false, message = "評價不存在" });
                }

                review.IsPinned = !review.IsPinned;
                await _context.SaveChangesAsync();

                _log.Log("Review", "TogglePin", reviewId.ToString(),
                    review.IsPinned ? "置頂評價" : "取消置頂評價");

                return Json(new
                {
                    success = true,
                    isPinned = review.IsPinned,
                    message = review.IsPinned ? "評價已置頂" : "已取消置頂"
                });
            }
            catch (Exception ex)
            {
                _log.Log("Review", "TogglePinError", reviewId.ToString(), ex.Message);
                return Json(new { success = false, message = "操作失敗" });
            }
        }

        // 檢舉評價（保留原有功能）
        [HttpPost]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report([FromForm] ReportReviewRequest request)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);

                // 檢查是否已經檢舉過
                var existingReport = await _context.ReviewReports
                    .FirstOrDefaultAsync(rr => rr.ReviewId == request.ReviewId && rr.ReporterId == userId);

                if (existingReport != null)
                {
                    return Json(new { success = false, message = "您已經檢舉過此評價" });
                }

                // 創建檢舉記錄
                var report = new ReviewReport
                {
                    ReviewId = request.ReviewId,
                    ReporterId = userId,
                    Reason = request.Reason,
                    Description = request.Description,
                    Harassment = request.Harassment,
                    Pornography = request.Pornography,
                    Threaten = request.Threaten,
                    Hatred = request.Hatred,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ReviewReports.Add(report);
                await _context.SaveChangesAsync();

                // 發送檢舉通知郵件
                try
                {
                    var review = await _context.Reviews
                        .Include(r => r.Product)
                        .FirstOrDefaultAsync(r => r.Id == request.ReviewId);

                    if (review != null)
                    {
                        var emailBody = $@"
                            收到新的評價檢舉：
                            
                            評價ID: {request.ReviewId}
                            商品: {review.Product.Name}
                            檢舉原因: {request.Reason}
                            詳細描述: {request.Description}
                            
                            檢舉類型:
                            - 騷擾: {request.Harassment}
                            - 色情: {request.Pornography}
                            - 威脅: {request.Threaten}
                            - 仇恨: {request.Hatred}
                            
                            請前往後台處理此檢舉。
                        ";

                        await _emailService.SendComplainMailAsync("評價檢舉通知", emailBody);
                    }
                }
                catch (Exception ex)
                {
                    _log.Log("Review", "ReportEmailError", request.ReviewId.ToString(), ex.Message);
                }

                _log.Log("Review", "Report", request.ReviewId.ToString(), $"檢舉評價：{request.Reason}");

                return Json(new { success = true, message = "檢舉已提交，我們會儘快處理" });
            }
            catch (Exception ex)
            {
                _log.Log("Review", "ReportError", request.ReviewId.ToString(), ex.Message);
                return Json(new { success = false, message = "檢舉提交失敗，請稍後重試" });
            }
        }

        // 獲取商品評價（API）- 改善版本
        [HttpGet]
        [Route("api/reviews/product/{productId}")]
        public async Task<IActionResult> GetProductReviews(int productId, int page = 1, int pageSize = 5, string sort = "latest")
        {
            try
            {
                var query = _context.Reviews
                    .Include(r => r.User)
                    .Where(r => r.ProductId == productId && r.IsVisible)
                    .AsQueryable();

                // 排序
                query = sort switch
                {
                    "latest" => query.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.CreatedAt),
                    "highscore" => query.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.Rating),
                    "helpful" => query.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.HelpfulCount),
                    _ => query.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.CreatedAt)
                };

                var totalItems = await query.CountAsync();
                var reviews = await query
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(r => new
                    {
                        r.Id,
                        r.Content,
                        r.Rating,
                        r.CreatedAt,
                        r.IsPinned,
                        r.HelpfulCount,
                        r.UnhelpfulCount,
                        UserName = r.UserName ?? r.User.Username,
                        HasImage = r.ImageData != null,
                        RelativeTime = r.RelativeTime,
                        RatingText = r.RatingText,
                        HasAdminReply = r.HasAdminReply,
                        AdminReply = r.AdminReply,
                        AdminReplyTime = r.AdminReplyTime
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = reviews,
                    totalItems = totalItems,
                    page = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalItems / pageSize)
                });
            }
            catch (Exception ex)
            {
                _log.Log("Review", "GetProductReviewsError", productId.ToString(), ex.Message);
                return Json(new { success = false, message = "獲取評價失敗" });
            }
        }

        // 獲取評價圖片
        [HttpGet]
        [Route("reviews/image/{reviewId}")]
        public async Task<IActionResult> GetReviewImage(int reviewId)
        {
            try
            {
                var review = await _context.Reviews.FindAsync(reviewId);
                if (review?.ImageData == null)
                {
                    return NotFound();
                }

                var contentType = review.ImageContentType ?? "image/jpeg";
                return File(review.ImageData, contentType);
            }
            catch
            {
                return NotFound();
            }
        }

        // 獲取用戶已購買但未評價的商品
        private async Task<List<dynamic>> GetPurchasedProductsForReview(int userId)
        {
            try
            {
                var purchasedProductIds = await _context.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Order.UserId == userId && oi.Order.OrderStatus == "已送達")
                    .Select(oi => oi.ProductId)
                    .Distinct()
                    .ToListAsync();

                var reviewedProductIds = await _context.Reviews
                    .Where(r => r.UserId == userId)
                    .Select(r => r.ProductId)
                    .ToListAsync();

                var unReviewedProductIds = purchasedProductIds.Except(reviewedProductIds);

                var products = await _context.Products
                    .Where(p => unReviewedProductIds.Contains(p.Id) && p.IsActive)
                    .Select(p => new { p.Id, p.Name, p.ImageUrl })
                    .ToListAsync();

                return products.Cast<dynamic>().ToList();
            }
            catch
            {
                return new List<dynamic>();
            }
        }
    }
}