using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using System.IO;
using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Services;
using ECommercePlatform.Models.ViewModels;
//using (var stream = new FileStream(path, FileMode.Create));

namespace ECommercePlatform.Controllers.Admin 
{
    [ApiController]
    [Route("api/admin/products")]
    [Authorize(AuthenticationSchemes = "EngineerCookie")]
    public class ProductsAdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly OperationLogService _log;
            public ProductsAdminController(ApplicationDbContext context, OperationLogService log)
        {
            _context = context;
            _log = log;
        }
        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_context.Products.ToList());
        }
        [HttpGet("{id}")]
        public IActionResult Get(int id)
        {
            var product = _context.Products.Find(id);
            return product == null ? NotFound() : Ok(product);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([FromForm] ProductUploadDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            try
            {
                var product = new Product
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    Price = dto.Price,
                    DiscountPrice = dto.DiscountPrice,
                    DiscountStart = dto.DiscountStart,
                    DiscountEnd = dto.DiscountEnd,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };
                if (dto.ImageFile != null && dto.ImageFile.Length > 0)
                {
                    var ext = Path.GetExtension(dto.ImageFile.FileName);
                    var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

                    if (allowed.Contains(ext.ToLower()))
                    {
                        var fileName = Guid.NewGuid().ToString("N") + ext;
                        var folder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                        if (!Directory.Exists(folder))
                            Directory.CreateDirectory(folder);

                        var path = Path.Combine(folder, fileName);
                        using var fs = new FileStream(path, FileMode.Create);
                        dto.ImageFile.CopyTo(fs);

                        product.ImageUrl = "/uploads/" + fileName;
                    }
                }

                _context.Products.Add(product);
                _context.SaveChanges();

                _log.Log("ProductsAdmin", "Create", product.Id.ToString(), "建立商品");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                return StatusCode(500, "建立商品失敗：" + ex.Message);
            }
        }
        [HttpPut("{id}")]
        public IActionResult Update(int id, Product updated)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();
            try
            {
                product.Name = updated.Name;
                product.Description = updated.Description;
                product.Price = updated.Price;
                product.ImageUrl = updated.ImageUrl;
                _context.SaveChanges();
                _log.Log("ProductsAdmin", "Update", id.ToString(), "更新商品");
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, "更新商品失敗：" + ex.Message);
            }
        }
        [HttpDelete("{id}")]
        public IActionResult Delete(int id)
        {
            var product = _context.Products.Find(id);
            if (product == null) return NotFound();
            try
            {
                _context.Products.Remove(product);
                _context.SaveChanges();
                _log.Log("ProductsAdmin", "Delete", id.ToString(), "刪除商品");
                _log.Log("ProductsAdmin", "Update", id.ToString(), "更新商品");
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, "刪除商品失敗：" + ex.Message);
            }
        }
    }
}
