using ECommercePlatform.Data;
using ECommercePlatform.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ECommercePlatform.Controllers
{
    public class EngineerController : Controller
    {
        private readonly ApplicationDbContext _context;
        public EngineerController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var engineer = _context.Users.FirstOrDefault(e => e.Username == username && e.Role == "Admin");
            if (engineer == null || !BCrypt.Net.BCrypt.Verify(password, engineer.PasswordHash))
            {
                ViewBag.Error = "帳號或密碼錯誤";
                return View();
            }
            var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, engineer.Username),
                    new Claim(ClaimTypes.Role, "Admin")
                };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
            };
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);
            return RedirectToAction("Index", "Home");
        }
        [HttpGet]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }

        [Authorize(Roles = "Admin")]
        public IActionResult Index()
        {
            return View();
        }
    }
}
