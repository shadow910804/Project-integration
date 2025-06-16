using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Services;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly OperationLogService _log;

        public AccountController(ApplicationDbContext context, EmailService emailService, OperationLogService log)
        {
            _context = context;
            _emailService = emailService;
            _log = log;
        }

        /// 登入頁面
        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        /// 處理登入
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    ViewBag.Message = "請輸入帳號和密碼";
                    return View();
                }

                // 查找用戶
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == username && u.IsActive && u.Role == "User");

                if (user == null)
                {
                    ViewBag.Message = "帳號不存在或已被停用";
                    return View();
                }

                // 驗證密碼
                bool passwordValid = false;

                if (user.PasswordHash.StartsWith("$2"))
                {
                    // BCrypt 格式
                    passwordValid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
                }
                else
                {
                    // 原有格式 (明文或簡單 hash)
                    passwordValid = user.PasswordHash == password;

                    // 如果驗證成功，順便升級為 BCrypt
                    if (passwordValid)
                    {
                        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
                        await _context.SaveChangesAsync();
                        _log.Log("Account", "PasswordUpgrade", user.Id.ToString(), "密碼格式升級為 BCrypt");
                    }
                }

                if (!passwordValid)
                {
                    ViewBag.Message = "密碼錯誤";
                    return View();
                }

                // 建立身份驗證
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim("UserId", user.Id.ToString()),
                    new Claim(ClaimTypes.Role, "User"),
                    new Claim(ClaimTypes.Email, user.Email)
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,principal, new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(24) // 延長到24小時
                });

                // 更新最後登入時間
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // 記錄操作日誌
                _log.Log("Account", "Login", user.Id.ToString(), $"用戶登入：{user.Username}");

                // 重導向
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ViewBag.Message = "登入過程發生錯誤，請稍後再試";
                _log.Log("Account", "LoginError", "", ex.Message);
                return View();
            }
        }

        /// 註冊頁面
        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        /// 處理註冊
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                // 檢查用戶名重複
                var existingUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == model.Username);

                if (existingUser != null)
                {
                    ViewBag.Message = "此用戶名已被使用";
                    return View(model);
                }

                // 檢查 Email 重複
                var existingEmail = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == model.Email);

                if (existingEmail != null)
                {
                    ViewBag.Message = "此 Email 已被註冊";
                    return View(model);
                }

                // 創建新用戶
                var user = new User
                {
                    Username = model.Username,
                    Email = model.Email,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                    Role = "User",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    FirstName = model.FirstName,
                    LastName = model.LastName
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();


                // 記錄操作日誌
                _log.Log("Account", "Register", $"新用戶註冊：{user.Username}");

                // 發送歡迎郵件（可選）
                try
                {
                    await _emailService.SendWelcomeEmailAsync(user.Email, user.Username);
                }
                catch (Exception ex)
                {
                    // 郵件發送失敗不影響註冊
                    _log.Log("Account", "WelcomeEmailError", user.Id.ToString(), ex.Message);
                }

                ViewBag.Message = "註冊成功！請登入";
                ViewBag.Success = true;
                return View(model);
            }
            catch (Exception ex)
            {
                ViewBag.Message = "註冊失敗，請稍後重試";
                _log.Log("Account", "RegisterError", "", ex.Message);
                return View(model);
            }
        }

        /// 登出
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            var userId = User.Claims.FirstOrDefault(c => c.Type == "UserId")?.Value;
            var username = User.Identity?.Name;

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!string.IsNullOrEmpty(userId))
            {
                _log.Log("Account", "Logout", userId, $"用戶登出：{username}");
            }

            return RedirectToAction("Index", "Home");
        }


        // 移除舊的 Project 路由相容性
        // 不再提供 /Project/SingIn 等舊路由
        // 如果需要重定向，可以在 Startup/Program.cs 中配置 URL 重寫規則
    }
}