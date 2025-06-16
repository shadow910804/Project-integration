using Microsoft.AspNetCore.Mvc;
using ECommercePlatform.Data;
using System.Linq;
using ECommercePlatform.Models;

namespace ECommercePlatform.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Index()
        {
            try
            {
                // 調試：檢查資料庫連接和資料
                var totalProducts = _context.Products.Count();
                var activeProducts = _context.Products.Count(p => p.IsActive);
                var allProductNames = _context.Products.Select(p => new { p.Name, p.IsActive, p.Price }).ToList();

                // 暫時不使用任何過濾條件，查看所有商品
                var featuredProducts = _context.Products
                    .OrderByDescending(p => p.Id)
                    .Take(10)
                    .ToList();

                // 傳遞調試信息到 View
                ViewBag.Debug_TotalProducts = totalProducts;
                ViewBag.Debug_ActiveProducts = activeProducts;
                ViewBag.Debug_AllProductNames = allProductNames;
                ViewBag.Debug_QueryResult = featuredProducts.Count;

                // 如果沒有商品，傳遞空列表
                return View(featuredProducts ?? new List<Product>());
            }
            catch (Exception ex)
            {
                // 如果有錯誤，顯示錯誤信息
                ViewBag.Error = ex.Message;
                return View(new List<Product>());
            }
        }
        public IActionResult Privacy()
        {
            return View();
        }
    }
}
