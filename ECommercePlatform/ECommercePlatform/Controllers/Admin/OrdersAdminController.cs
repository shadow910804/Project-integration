using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECommercePlatform.Data;
using ECommercePlatform.Models;

    namespace ECommercePlatform.Controllers.Admin;
[ApiController]
[Route("api/admin/orders")]
[Authorize(AuthenticationSchemes = "EngineerCookie")]
public class OrdersAdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    public OrdersAdminController(ApplicationDbContext context)
    {
        _context = context;
    }
    [HttpGet]
    public IActionResult GetAll([FromQuery] string? user, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
                                [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var query = _context.Orders
            .Include(o => o.User)
            .AsQueryable();
        if (!string.IsNullOrEmpty(user))
        {
            query = query.Where(o => o.User.Username.Contains(user));
        }
        if (from.HasValue)
        {
            query = query.Where(o => o.OrderDate >= from.Value);
        }
        if (to.HasValue)
        {
            query = query.Where(o => o.OrderDate <= to.Value);
        }
        var totalCount = query.Count();
        var result = query
            .OrderByDescending(o => o.OrderDate)// 按訂單日期排序（最新的先）
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.OrderDate,
                o.TotalAmount,
                o.OrderStatus,
                o.PaymentMethod,
                o.ShippingAddress,
                UserName = o.User.Username
            })
            .ToList();
        return Ok(new
        {
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            Data = result
        });
    }
    [HttpGet("{id}")]
    public IActionResult GetDetails(int id)
    {
        var order = _context.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .Include(o => o.User)
            .FirstOrDefault(o => o.Id == id);
        if (order == null) return NotFound();
        return Ok(new
        {
            order.Id,
            order.OrderDate,
            order.TotalAmount,
            order.OrderStatus,
            order.PaymentMethod,
            order.ShippingAddress,
            order.PaymentVerified,
            order.Tags,
            User = new { order.User.Id, order.User.Username, order.User.Email },
            Items = order.OrderItems.Select(i => new
            {
                i.ProductId,
                ProductName = i.Product.Name,
                i.Quantity,
                i.UnitPrice
            })
        });
    }
    [HttpPut("{id}/status")]
    public IActionResult UpdateStatus(int id, [FromBody] string status)
    {
        var order = _context.Orders.Find(id);
        if (order == null) return NotFound();
        var validStatuses = new[] { "Pending", "Shipped", "Delivered", "Cancelled" };
        if (!validStatuses.Contains(status)) return BadRequest("Invalid status");
        order.OrderStatus = status;
        _context.SaveChanges();
        return Ok();
    }
    [HttpPut("{id}/payment-verify")]
    public IActionResult VerifyPayment(int id, [FromBody] bool verified)
    {
        var order = _context.Orders.Find(id);
        if (order == null) return NotFound();
        order.PaymentVerified = verified;
        _context.SaveChanges();
        return Ok();
    }
}
