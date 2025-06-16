using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommercePlatform.Data;
using System.Linq;

namespace ECommercePlatform.Controllers.Admin
{
    [Authorize(AuthenticationSchemes = "EngineerCookie")]
    [Route("admin/orders")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;
        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet("")]
        public IActionResult Index()
        {
            var orders = _context.Orders
                .Include(o => o.User)
                .OrderByDescending(o => o.OrderDate)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.TotalAmount,
                    o.OrderStatus,
                    o.PaymentMethod,
                    UserName = o.User.Username
                })
                .ToList();
            return View(orders);
        }
        [HttpGet("details/{id}")]
        public IActionResult Details(int id)
        {
            var order = _context.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                .Where(o => o.Id == id)
                .Select(o => new
                {
                    o.Id,
                    o.OrderDate,
                    o.TotalAmount,
                    o.OrderStatus,
                    o.PaymentMethod,
                    o.ShippingAddress,
                    o.PaymentVerified,
                    User = new { o.User.Username, o.User.Email },
                    Items = o.OrderItems.Select(i => new
                    {
                        i.ProductId,
                        ProductName = i.Product.Name,
                        i.Quantity,
                        i.UnitPrice
                    })
                })
                .FirstOrDefault();
            if (order == null)
                return NotFound();
            return View(order);
        }
    }
}
