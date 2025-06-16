using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ECommercePlatform.Data;
using ECommercePlatform.Services;

namespace ECommercePlatform.Controllers.Admin
{
    [Authorize(AuthenticationSchemes = "EngineerCookie")]
    [Route("admin/push")]
    public class PushController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly FcmService _fcm;
        public PushController(ApplicationDbContext context)
        {
            _context = context;
            _fcm = new FcmService(); // NOTE: Use DI if needed
        }
        [HttpGet("")]
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost("")]
        public async Task<IActionResult> Index(string title, string body)
        {
            var tokens = _context.DeviceTokens.Select(t => t.Token).ToList();
            int success = 0;
            foreach (var token in tokens)
            {
                var result = await _fcm.SendNotificationAsync(token, title, body);
                if (result) success++;
            }
            ViewBag.Message = $"推播完成，共發送 {success} 則通知。";
            return View();
        }
    }
}
