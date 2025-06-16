using Microsoft.AspNetCore.Mvc;
using ECommercePlatform.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ECommercePlatform.Data;
using ECommercePlatform.Models.ViewModels;

namespace ECommercePlatform.Controllers
{
    [Authorize(AuthenticationSchemes = "EngineerCookie")]
    [Route("admin/users")]
    public class AdminUsersController : Controller
    {
        private readonly ApplicationDbContext _context;
        public AdminUsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("")]
        public async Task<IActionResult> Index(string? keyword, int page = 1, int pageSize = 10)
        {
            var query = _context.Users.AsQueryable();
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(u => u.Username.Contains(keyword) || u.Email.Contains(keyword));
            }
            var totalItems = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .OrderBy(u => u.Id)// ¥[¤W±Æ§Ç
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
            return View(result);
        }

        [HttpGet("create")]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost("create")]
        public async Task<IActionResult> Create(User user)
        {
            if (ModelState.IsValid)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
                user.CreatedAt = DateTime.UtcNow;
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost("edit/{id}")]
        public async Task<IActionResult> Edit(User user)
        {
            if (ModelState.IsValid)
            {
                var existing = await _context.Users.FindAsync(user.Id);
                if (existing == null) return NotFound();
                existing.Username = user.Username;
                existing.Email = user.Email;
                existing.IsActive = user.IsActive;
                existing.Address = user.Address;
                existing.PhoneNumber = user.PhoneNumber;
                existing.FirstName = user.FirstName;
                existing.LastName = user.LastName;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(user);
        }

        [HttpGet("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
