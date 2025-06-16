using Microsoft.AspNetCore.Mvc;
using ECommercePlatform.Data;
﻿using Microsoft.AspNetCore.Authorization;

namespace ECommercePlatform.Controllers
{
    [Authorize(Roles = "Admin")]
    public class StatisticsController : Controller
    {
        private readonly ApplicationDbContext _context;
        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            var salesData = _context.Orders
                .GroupBy(o => o.OrderDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalSales = g.Sum(o => o.TotalAmount),
                    OrderCount = g.Count()
                }).ToList();
            return View(salesData);
        }
    }
}
