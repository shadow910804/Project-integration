using DocumentFormat.OpenXml.Spreadsheet;
using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;
using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Controllers
{
    public class MemberController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly OperationLogService _log;

        public MemberController(ApplicationDbContext context, EmailService emailService, OperationLogService log)
        {
            _context = context;
            _emailService = emailService;
            _log = log;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Login");
            }
            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound();
            }
            return View(user);
        }


        /// 用戶資料頁面
        [HttpGet]
        public IActionResult Profile()
        {
            return View();
        }

        /// 更新用戶資料
        [HttpPost]
        [Authorize(Roles = "User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(FixProfile model)
        {
            if (!User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Login");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }

            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var user = await _context.Users.FindAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            // 只更新允許用戶修改的欄位
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.PhoneNumber = model.PhoneNumber;
            user.Address = model.Address;

            try
            {
                await _context.SaveChangesAsync();
                _log.Log("Account", "UpdateProfile", userId.ToString(), "更新個人資料");
                ViewBag.Message = "資料更新成功";
                ViewBag.Success = true;
            }
            catch (Exception ex)
            {
                ViewBag.Message = "更新失敗，請稍後再試";
                _log.Log("Account", "UpdateProfileError", userId.ToString(), ex.Message);
            }

            return View(user);
        }

        [HttpGet]
        [Authorize(Roles = "User")]
        public IActionResult Password()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = "User")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Password(FixPassword fix)
        {
            if (!ModelState.IsValid)
            {
                return View();
            }

            var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
            var user = await _context.Users.FindAsync(userId);
            bool passwordValid = false;
            passwordValid = BCrypt.Net.BCrypt.Verify(fix.OldPassword, user.PasswordHash);

            if (!passwordValid)
            {
                ViewBag.Message = "舊密碼有誤";
                return View();
            }
            try
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(fix.NewPassword);

                await _context.SaveChangesAsync();
                _log.Log("Account", "UpdatePassword", user.Id.ToString(), "更新密碼");
                ViewBag.Message = "資料更新成功";
                ViewBag.Success = true;

            }
            catch (Exception ex)
            {
                _log.Log("Member", "UpdatePasswordError", user.Id.ToString(), ex.Message);
                ViewBag.Message = "更新失敗，請稍後再試";
            }
            return View();
        }
    }
}
