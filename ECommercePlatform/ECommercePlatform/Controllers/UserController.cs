using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Services;
using ECommercePlatform.Models.ViewModels;

namespace ECommercePlatform.Controllers
{
    //統一的用戶管理控制器（整合前台會員功能和後台管理功能）
    public class UserController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly OperationLogService _logService;

        public UserController(ApplicationDbContext context, OperationLogService logService)
        {
            _context = context;
            _logService = logService;
        }

        #region 前台會員功能 (Member相關)

        //會員中心首頁
        [HttpGet("/Member")]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        public async Task<IActionResult> MemberIndex()
        {
            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();
            return View("Member/Index", user);
        }

        //會員資料頁面
        [HttpGet("/Member/Profile")]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        public async Task<IActionResult> MemberProfile()
        {
            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();
            return View("Member/Profile", user);
        }

        //更新會員資料
        [HttpPost("/Member/Profile")]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMemberProfile(User updated)
        {
            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // 只更新允許用戶修改的欄位
            user.FirstName = updated.FirstName;
            user.LastName = updated.LastName;
            user.PhoneNumber = updated.PhoneNumber;
            user.Address = updated.Address;

            try
            {
                await _context.SaveChangesAsync();
                _logService.Log("User", "UpdateProfile", userId.ToString(), "更新個人資料");
                TempData["Success"] = "資料更新成功";
            }
            catch (Exception ex)
            {
                _logService.Log("User", "UpdateProfileError", userId.ToString(), ex.Message);
                TempData["Error"] = "更新失敗，請稍後再試";
            }

            return View("Member/Profile", user);
        }

        //會員訂單列表
        [HttpGet("/Member/Orders")]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        public async Task<IActionResult> MemberOrders()
        {
            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
            return View("Member/Orders", orders);
        }

        //會員訂單詳情
        [HttpGet("/Member/Orders/{id}")]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        public async Task<IActionResult> MemberOrderDetail(int id)
        {
            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

            if (order == null) return NotFound();
            return View("Member/OrderDetail", order);
        }

        //會員評價列表
        [HttpGet("/Member/Reviews")]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        public async Task<IActionResult> MemberReviews()
        {
            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var reviews = await _context.Reviews
                .Include(r => r.Product)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();
            return View("Member/Reviews", reviews);
        }

        //刪除會員評價
        [HttpGet("/Member/Reviews/Delete/{id}")]
        [Authorize(AuthenticationSchemes = "UserCookie")]
        public async Task<IActionResult> DeleteMemberReview(int id)
        {
            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var review = await _context.Reviews
                .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

            if (review == null) return NotFound();

            _context.Reviews.Remove(review);
            await _context.SaveChangesAsync();

            _logService.Log("User", "DeleteReview", id.ToString(), "刪除評價");
            TempData["Success"] = "評價已刪除";

            return RedirectToAction("MemberReviews");
        }

        #endregion

        #region 後台用戶管理功能 (Admin相關)

        //後台用戶列表
        [HttpGet("/admin/users")]
        [Authorize(AuthenticationSchemes = "EngineerCookie")]
        public async Task<IActionResult> AdminIndex(string? keyword, int page = 1, int pageSize = 10)
        {
            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(u => u.Username.Contains(keyword) || u.Email.Contains(keyword));
            }

            var totalItems = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new PagedResult<User>
            {
                Items = users,
                TotalItems = totalItems,
                PageNumber = page,
                PageSize = pageSize,
            };

            ViewBag.Keyword = keyword;
            return View("Admin/Index", result);
        }

        //後台新增用戶頁面
        [HttpGet("/admin/users/create")]
        [Authorize(AuthenticationSchemes = "EngineerCookie")]
        public IActionResult AdminCreate()
        {
            return View("Admin/Create");
        }

        //後台新增用戶處理
        [HttpPost("/admin/users/create")]
        [Authorize(AuthenticationSchemes = "EngineerCookie")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminCreate(User user)
        {
            if (ModelState.IsValid)
            {
                // 檢查用戶名重複
                if (await _context.Users.AnyAsync(u => u.Username == user.Username))
                {
                    ModelState.AddModelError("Username", "用戶名已存在");
                    return View("Admin/Create", user);
                }

                // 檢查Email重複
                if (await _context.Users.AnyAsync(u => u.Email == user.Email))
                {
                    ModelState.AddModelError("Email", "Email已被註冊");
                    return View("Admin/Create", user);
                }

                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
                user.CreatedAt = DateTime.UtcNow;
                user.IsActive = true;

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                _logService.Log("User", "AdminCreate", user.Id.ToString(), $"管理員建立用戶: {user.Username}");
                TempData["Success"] = "用戶建立成功";

                return RedirectToAction("AdminIndex");
            }
            return View("Admin/Create", user);
        }

        //後台編輯用戶頁面
        [HttpGet("/admin/users/edit/{id}")]
        [Authorize(AuthenticationSchemes = "EngineerCookie")]
        public async Task<IActionResult> AdminEdit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View("Admin/Edit", user);
        }

        //後台編輯用戶處理
        [HttpPost("/admin/users/edit/{id}")]
        [Authorize(AuthenticationSchemes = "EngineerCookie")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdminEdit(int id, User updatedUser)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (ModelState.IsValid)
            {
                // 檢查用戶名重複（排除自己）
                if (await _context.Users.AnyAsync(u => u.Username == updatedUser.Username && u.Id != id))
                {
                    ModelState.AddModelError("Username", "用戶名已被其他人使用");
                    return View("Admin/Edit", updatedUser);
                }

                // 檢查Email重複（排除自己）
                if (await _context.Users.AnyAsync(u => u.Email == updatedUser.Email && u.Id != id))
                {
                    ModelState.AddModelError("Email", "Email已被其他人使用");
                    return View("Admin/Edit", updatedUser);
                }

                user.Username = updatedUser.Username;
                user.Email = updatedUser.Email;
                user.IsActive = updatedUser.IsActive;
                user.Address = updatedUser.Address;
                user.PhoneNumber = updatedUser.PhoneNumber;
                user.FirstName = updatedUser.FirstName;
                user.LastName = updatedUser.LastName;
                user.Role = updatedUser.Role;

                await _context.SaveChangesAsync();

                _logService.Log("User", "AdminUpdate", id.ToString(), $"管理員更新用戶: {user.Username}");
                TempData["Success"] = "用戶更新成功";

                return RedirectToAction("AdminIndex");
            }
            return View("Admin/Edit", updatedUser);
        }

        //後台刪除用戶
        [HttpGet("/admin/users/delete/{id}")]
        [Authorize(AuthenticationSchemes = "EngineerCookie")]
        public async Task<IActionResult> AdminDelete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            // 檢查是否有相關訂單
            var hasOrders = await _context.Orders.AnyAsync(o => o.UserId == id);
            if (hasOrders)
            {
                TempData["Error"] = "該用戶有相關訂單記錄，無法刪除。建議將用戶設為停用狀態。";
                return RedirectToAction("AdminIndex");
            }

            var username = user.Username;
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            _logService.Log("User", "AdminDelete", id.ToString(), $"管理員刪除用戶: {username}");
            TempData["Success"] = "用戶已刪除";

            return RedirectToAction("AdminIndex");
        }

        #endregion

        #region API 方法

        //API: 獲取用戶列表
        [HttpGet("/api/users")]
        [Authorize(AuthenticationSchemes = "EngineerCookie")]
        public async Task<IActionResult> GetUsers([FromQuery] string? keyword, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                if (!string.IsNullOrEmpty(keyword))
                {
                    query = query.Where(u => u.Username.Contains(keyword) ||
                                           u.Email.Contains(keyword) ||
                                           (u.FirstName != null && u.FirstName.Contains(keyword)) ||
                                           (u.LastName != null && u.LastName.Contains(keyword)));
                }

                var totalCount = await query.CountAsync();
                var users = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Email,
                        u.PhoneNumber,
                        u.FirstName,
                        u.LastName,
                        u.Address,
                        u.IsActive,
                        u.CreatedAt,
                        u.Role
                    })
                    .ToListAsync();

                return Json(new
                {
                    success = true,
                    data = users,
                    totalCount = totalCount,
                    page = page,
                    pageSize = pageSize,
                    totalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logService.Log("User", "GetUsersError", "", ex.Message);
                return Json(new { success = false, message = "獲取用戶列表失敗" });
            }
        }

        //API: 批量更新用戶狀態
        [HttpPost("/api/users/batch-status")]
        [Authorize(AuthenticationSchemes = "EngineerCookie")]
        public async Task<IActionResult> BatchUpdateStatus([FromBody] BatchStatusRequest request)
        {
            try
            {
                if (request.UserIds == null || !request.UserIds.Any())
                {
                    return BadRequest(new { message = "請提供要更新的用戶 ID" });
                }

                var users = await _context.Users.Where(u => request.UserIds.Contains(u.Id)).ToListAsync();
                if (!users.Any())
                {
                    return NotFound(new { message = "找不到指定的用戶" });
                }

                foreach (var user in users)
                {
                    user.IsActive = request.IsActive;
                }

                await _context.SaveChangesAsync();

                var action = request.IsActive ? "啟用" : "停用";
                _logService.Log("User", "BatchUpdate", string.Join(",", request.UserIds),
                    $"批量{action}用戶，共 {users.Count} 位");

                return Json(new
                {
                    success = true,
                    message = $"成功{action} {users.Count} 位用戶"
                });
            }
            catch (Exception ex)
            {
                _logService.Log("User", "BatchUpdateError", "", ex.Message);
                return Json(new { success = false, message = "批量更新失敗" });
            }
        }

        #endregion
    }

    // DTO 類別
    public class BatchStatusRequest
    {
        public List<int> UserIds { get; set; } = new();
        public bool IsActive { get; set; }
    }
}