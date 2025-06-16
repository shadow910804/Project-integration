using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommercePlatform.Data;
using ECommercePlatform.Models;

namespace ECommercePlatform.Controllers.Admin
{
    [Authorize(AuthenticationSchemes = "EngineerCookie")]
    [Route("admin/engineers")]
    public class EngineersController : Controller
    {
        private readonly ApplicationDbContext _context;
        public EngineersController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            var engineers = await _context.Engineers.ToListAsync();
            return View(engineers);
        }
        [HttpGet("create")]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost("create")]
        public async Task<IActionResult> Create(Engineer engineer)
        {
            if (ModelState.IsValid)
            {
                engineer.PasswordHash = BCrypt.Net.BCrypt.HashPassword(engineer.PasswordHash);
                _context.Engineers.Add(engineer);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(engineer);
        }
        [HttpGet("edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var engineer = await _context.Engineers.FindAsync(id);
            if (engineer == null) return NotFound();
            return View(engineer);
        }
        [HttpPost("edit/{id}")]
        public async Task<IActionResult> Edit(Engineer updated)
        {
            if (ModelState.IsValid)
            {
                var original = await _context.Engineers.FindAsync(updated.Id);
                if (original == null) return NotFound();
                original.Username = updated.Username;
                original.Email = updated.Email;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(updated);
        }
        [HttpGet("delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var engineer = await _context.Engineers.FindAsync(id);
            if (engineer == null) return NotFound();
            _context.Engineers.Remove(engineer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
