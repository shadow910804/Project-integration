using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommercePlatform.Data;

namespace ECommercePlatform.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/dashboard")]
    [Authorize(AuthenticationSchemes = "EngineerCookie")]
    public class DashboardController : ControllerBase
    {
        [HttpGet("charts")]
        public IActionResult GetCharts()
        {
            var today = DateTime.Today;
            var dailyOrderCounts = _context.Orders
                .Where(o => o.OrderDate >= today.AddDays(-6))
                .GroupBy(o => o.OrderDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    date = g.Key.ToString("MM-dd"),
                    count = g.Count()
                })
                .ToList();
            var dailyRevenue = _context.Orders
                .Where(o => o.OrderDate >= today.AddDays(-6))
                .GroupBy(o => o.OrderDate.Date)
                .OrderBy(g => g.Key)
                .Select(g => new
                {
                    date = g.Key.ToString("MM-dd"),
                    amount = g.Sum(x => x.TotalAmount)
                })
                .ToList();
            var statusDistribution = _context.Orders
                .GroupBy(o => o.OrderStatus)
                .Select(g => new
                {
                    status = g.Key,
                    count = g.Count()
                })
                .ToList();
            return Ok(new
            {
                dailyOrderCounts,
                dailyRevenue,
                statusDistribution
            });
        }
        private readonly ApplicationDbContext _context;
        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet("stats")]
        public IActionResult GetStats()
        {
            var today = DateTime.Today;
            var stats = new
            {
                TotalUsers = _context.Users.Count(),
                TotalOrders = _context.Orders.Count(),
                TotalRevenue = _context.Orders.Sum(o => (decimal?)o.TotalAmount) ?? 0,
                TotalProducts = _context.Products.Count(),
                TodayOrders = _context.Orders.Count(o => o.OrderDate.Date == today),
                TodayRevenue = _context.Orders
                    .Where(o => o.OrderDate.Date == today)
                    .Sum(o => (decimal?)o.TotalAmount) ?? 0
            };
            return Ok(stats);
        }
    }
}
