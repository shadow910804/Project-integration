using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using iText.Html2pdf;
using ECommercePlatform.Data;
using System.IO;
using System.Linq;
using System.Text;



    namespace ECommercePlatform.Controllers.Admin
    {
        [ApiController]
        [Route("api/admin/orders/export")]
        [Authorize(AuthenticationSchemes = "EngineerCookie")]
        public class OrdersExportController : ControllerBase
        {
            private readonly ApplicationDbContext _context;

        public OrdersExportController(ApplicationDbContext context)
            {
                _context = context;
            }
            [HttpGet]
            public IActionResult Export([FromQuery] string format = "excel")
            {
            using var pdfStream = new MemoryStream();
            using var workbook = new XLWorkbook();
            using var stream = new MemoryStream();
            try
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
                            o.ShippingAddress,
                            UserName = o.User.Username
                        })
                        .ToList();
                    if (format == "pdf")
                    {
                        var htmlBuilder = new StringBuilder();
                        htmlBuilder.AppendLine("<html><head><meta charset='UTF-8'></head><body>");
                        htmlBuilder.AppendLine("<h1>訂單報表</h1>");
                        htmlBuilder.AppendLine("<table border='1' cellspacing='0' cellpadding='5'>");
                        htmlBuilder.AppendLine("<tr><th>訂單編號</th><th>用戶</th><th>總金額</th><th>狀態</th><th>建立時間</th></tr>");
                        foreach (var order in orders)
                        {
                            htmlBuilder.AppendLine($"<tr><td>{order.Id}</td><td>{order.UserName}</td><td>{order.TotalAmount:C}</td><td>{order.OrderStatus}</td><td>{order.OrderDate:yyyy-MM-dd HH:mm}</td></tr>");
                        }
                        htmlBuilder.AppendLine("</table></body></html>");

                        using var htmlStream = new MemoryStream(Encoding.UTF8.GetBytes(htmlBuilder.ToString()));
                        HtmlConverter.ConvertToPdf(htmlStream, pdfStream);
                        pdfStream.Position = 0;
                        return File(pdfStream.ToArray(), "application/pdf", "orders.pdf");
                    }
                    else
                    {
                        var worksheet = workbook.Worksheets.Add("訂單報表");
                        worksheet.Cell(1, 1).Value = "訂單編號";
                        worksheet.Cell(1, 2).Value = "用戶";
                        worksheet.Cell(1, 3).Value = "總金額";
                        worksheet.Cell(1, 4).Value = "狀態";
                        worksheet.Cell(1, 5).Value = "建立時間";
                        for (int i = 0; i < orders.Count; i++)
                        {
                            var row = i + 2;
                            var order = orders[i];
                            worksheet.Cell(row, 1).Value = order.Id;
                            worksheet.Cell(row, 2).Value = order.UserName;
                            worksheet.Cell(row, 3).Value = order.TotalAmount;
                            worksheet.Cell(row, 4).Value = order.OrderStatus;
                            worksheet.Cell(row, 5).Value = order.OrderDate.ToString("yyyy-MM-dd HH:mm");
                        }
                        workbook.SaveAs(stream);
                        stream.Position = 0;
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "orders.xlsx");
                    }
                }
                catch (Exception ex)
                {
                    return StatusCode(500, "匯出失敗：" + ex.Message);
                }
            }
        }
    }
