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
                // �ոաG�ˬd��Ʈw�s���M���
                var totalProducts = _context.Products.Count();
                var activeProducts = _context.Products.Count(p => p.IsActive);
                var allProductNames = _context.Products.Select(p => new { p.Name, p.IsActive, p.Price }).ToList();

                // �Ȯɤ��ϥΥ���L�o����A�d�ݩҦ��ӫ~
                var featuredProducts = _context.Products
                    .OrderByDescending(p => p.Id)
                    .Take(10)
                    .ToList();

                // �ǻ��ոիH���� View
                ViewBag.Debug_TotalProducts = totalProducts;
                ViewBag.Debug_ActiveProducts = activeProducts;
                ViewBag.Debug_AllProductNames = allProductNames;
                ViewBag.Debug_QueryResult = featuredProducts.Count;

                // �p�G�S���ӫ~�A�ǻ��ŦC��
                return View(featuredProducts ?? new List<Product>());
            }
            catch (Exception ex)
            {
                // �p�G�����~�A��ܿ��~�H��
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
