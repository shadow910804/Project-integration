using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Services;
using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderService _orderService;
        private readonly IConfiguration _configuration;
        private readonly OperationLogService _log;

        public PaymentController(
            ApplicationDbContext context,
            IOrderService orderService,
            IConfiguration configuration,
            OperationLogService log)
        {
            _context = context;
            _orderService = orderService;
            _configuration = configuration;
            _log = log;
        }

        //結帳頁面
        [HttpGet]
        [Authorize(Roles = "User")]
        [Route("checkout")]
        public async Task<IActionResult> Checkout()
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);

                // 獲取購物車商品
                var cartItems = await _context.CartItems
                    .Include(c => c.Product)
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                if (!cartItems.Any())
                {
                    TempData["Error"] = "購物車為空，無法結帳";
                    return RedirectToAction("Index", "Cart");
                }

                // 獲取用戶資訊
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // 計算總金額
                decimal subtotal = 0;
                var checkoutItems = new List<CheckoutItemViewModel>();

                foreach (var item in cartItems)
                {
                    if (!item.Product.IsActive)
                    {
                        TempData["Error"] = $"商品 {item.Product.Name} 已下架，請先移除";
                        return RedirectToAction("Index", "Cart");
                    }

                    var unitPrice = item.Product.CurrentPrice;
                    var itemTotal = unitPrice * item.Quantity;
                    subtotal += itemTotal;

                    checkoutItems.Add(new CheckoutItemViewModel
                    {
                        ProductId = item.ProductId,
                        ProductName = item.Product.Name,
                        ProductImage = item.Product.ImageUrl,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        Subtotal = itemTotal,
                        HasDiscount = item.Product.HasDiscount
                    });
                }

                var shippingCost = subtotal >= 1000 ? 0 : 100;
                var total = subtotal + shippingCost;

                var model = new CheckoutViewModel
                {
                    User = user,
                    Items = checkoutItems,
                    Subtotal = subtotal,
                    ShippingCost = shippingCost,
                    Total = total,
                    PaymentMethods = GetAvailablePaymentMethods()
                };

                return View(model);
            }
            catch (Exception ex)
            {
                _log.Log("Payment", "CheckoutError", "", ex.Message);
                TempData["Error"] = "結帳頁面載入失敗";
                return RedirectToAction("Index", "Cart");
            }
        }

        //處理結帳提交
        [HttpPost]
        [Authorize(Roles = "User")]
        [ValidateAntiForgeryToken]
        [Route("checkout")]
        public async Task<IActionResult> ProcessCheckout(CheckoutSubmitModel model)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    TempData["Error"] = "請填寫完整的收件資訊";
                    return RedirectToAction("Checkout");
                }

                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);

                // 創建訂單請求
                var orderRequest = new CreateOrderRequest
                {
                    UserId = userId,
                    ShippingAddress = model.ShippingAddress,
                    PaymentMethod = model.PaymentMethod,
                    ShippingMethod = model.ShippingMethod ?? "standard"
                };

                // 使用訂單服務創建訂單
                var orderResult = await _orderService.CreateOrderAsync(orderRequest);

                if (!orderResult.IsSuccess)
                {
                    TempData["Error"] = orderResult.Message;
                    return RedirectToAction("Checkout");
                }

                // 根據支付方式處理
                return model.PaymentMethod switch
                {
                    "信用卡" => await ProcessCreditCardPayment(orderResult.OrderId!.Value),
                    "Line Pay" => await ProcessLinePayPayment(orderResult.OrderId!.Value),
                    "貨到付款" => ProcessCashOnDelivery(orderResult.OrderId!.Value),
                    _ => throw new NotSupportedException("不支援的支付方式")
                };
            }
            catch (Exception ex)
            {
                _log.Log("Payment", "ProcessCheckoutError", "", ex.Message);
                TempData["Error"] = "訂單處理失敗，請稍後重試";
                return RedirectToAction("Checkout");
            }
        }

        //處理信用卡支付（藍新金流）
        private async Task<IActionResult> ProcessCreditCardPayment(int orderId)
        {
            try
            {
                var order = await _context.Orders
                    .Include(o => o.User)
                    .FirstOrDefaultAsync(o => o.Id == orderId);

                if (order == null)
                {
                    return NotFound();
                }

                var newebPaySettings = _configuration.GetSection("NewebPay");
                var merchantId = newebPaySettings["MerchantID"] ?? "your-merchant-id";
                var hashKey = newebPaySettings["HashKey"] ?? "your-hash-key";
                var hashIV = newebPaySettings["HashIV"] ?? "your-hash-iv";

                // 準備交易資料
                var tradeInfo = new
                {
                    MerchantID = merchantId,
                    RespondType = "JSON",
                    TimeStamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                    Version = "2.0",
                    MerchantOrderNo = orderId.ToString(),
                    Amt = ((int)order.TotalAmount).ToString(),
                    ItemDesc = $"Ez購,Ez Life 訂單 #{orderId}",
                    Email = order.User.Email,
                    LoginType = "0",
                    NotifyURL = Url.Action("Notify", "Payment", null, Request.Scheme),
                    ReturnURL = Url.Action("Return", "Payment", null, Request.Scheme),
                    ClientBackURL = Url.Action("OrderConfirm", "Payment", new { id = orderId }, Request.Scheme)
                };

                var tradeInfoJson = JsonSerializer.Serialize(tradeInfo);
                var encryptedTradeInfo = EncryptAES(tradeInfoJson, hashKey, hashIV);
                var tradeSha = GenerateCheckValue(encryptedTradeInfo, hashKey, hashIV);

                var paymentModel = new NewebPayViewModel
                {
                    MerchantID = merchantId,
                    TradeInfo = encryptedTradeInfo,
                    TradeSha = tradeSha,
                    Version = "2.0",
                    Order = order
                };

                return View("NewebPay", paymentModel);
            }
            catch (Exception ex)
            {
                _log.Log("Payment", "CreditCardError", orderId.ToString(), ex.Message);
                TempData["Error"] = "支付頁面載入失敗";
                return RedirectToAction("OrderConfirm", new { id = orderId });
            }
        }

        /// 處理 Line Pay 支付
        private async Task<IActionResult> ProcessLinePayPayment(int orderId)
        {
            // 整合 Line Pay API
            TempData["Info"] = "Line Pay 整合開發中，暫時使用測試模式";
            await _orderService.ConfirmPaymentAsync(orderId, "LINE_PAY_TEST");
            return RedirectToAction("OrderConfirm", new { id = orderId });
        }

        /// 處理貨到付款
        private IActionResult ProcessCashOnDelivery(int orderId)
        {
            // 貨到付款不需要線上支付，直接確認訂單
            return RedirectToAction("OrderConfirm", new { id = orderId });
        }

        /// 藍新金流支付通知回調
        [HttpPost]
        [Route("payment/notify")]
        public async Task<IActionResult> Notify([FromForm] string TradeInfo)
        {
            try
            {
                var newebPaySettings = _configuration.GetSection("NewebPay");
                var hashKey = newebPaySettings["HashKey"] ?? "your-hash-key";
                var hashIV = newebPaySettings["HashIV"] ?? "your-hash-iv";

                // 解密交易資料
                var decryptedData = DecryptAES(TradeInfo, hashKey, hashIV);
                var result = JsonSerializer.Deserialize<Dictionary<string, object>>(decryptedData);

                var status = result.ContainsKey("Status") ? result["Status"].ToString() : null;
                var merchantOrderNo = ExtractMerchantOrderNo(result);

                if (status == "SUCCESS" && int.TryParse(merchantOrderNo, out int orderId))
                {
                    // 確認付款成功
                    var paymentReference = ExtractPaymentReference(result);
                    await _orderService.ConfirmPaymentAsync(orderId, paymentReference);

                    _log.Log("Payment", "NotifySuccess", orderId.ToString(),
                        $"支付確認成功，參考號: {paymentReference}");
                }
                else
                {
                    _log.Log("Payment", "NotifyFailed", merchantOrderNo ?? "Unknown",
                        $"支付失敗，狀態: {status}");
                }

                return Content("1|OK"); // 藍新金流要求的回應格式
            }
            catch (Exception ex)
            {
                _log.Log("Payment", "NotifyError", "", ex.Message);
                return Content("0|ERROR");
            }
        }

        /// 支付完成返回頁面
        [HttpGet]
        [Route("payment/return")]
        [HttpGet]
        [Route("payment/return")]
        public IActionResult Return(string? TradeInfo = null) // 移除 async，因為沒有 await
        {
            if (!string.IsNullOrEmpty(TradeInfo))
            {
                try
                {
                    var newebPaySettings = _configuration.GetSection("NewebPay");
                    var hashKey = newebPaySettings["HashKey"] ?? "your-hash-key";
                    var hashIV = newebPaySettings["HashIV"] ?? "your-hash-iv";

                    var decryptedData = DecryptAES(TradeInfo, hashKey, hashIV);
                    var result = JsonSerializer.Deserialize<Dictionary<string, object>>(decryptedData);
                    var merchantOrderNo = ExtractMerchantOrderNo(result!); // 加上 null-forgiving operator

                    if (int.TryParse(merchantOrderNo, out int orderId))
                    {
                        return RedirectToAction("OrderConfirm", new { id = orderId });
                    }
                }
                catch (Exception ex)
                {
                    _log.Log("Payment", "ReturnError", "", ex.Message);
                }
            }

            return RedirectToAction("Index", "Home");
        }

        //訂單確認頁面
        [HttpGet]
        [Authorize(Roles = "User")]
        [Route("order/confirm/{id}")]
        public async Task<IActionResult> OrderConfirm(int id)
        {
            try
            {
                var userId = int.Parse(User.Claims.First(c => c.Type == "UserId").Value);
                var orderDetails = await _orderService.GetOrderDetailsAsync(id, userId);

                if (orderDetails == null)
                {
                    return NotFound();
                }

                return View(orderDetails);
            }
            catch (Exception ex)
            {
                _log.Log("Payment", "OrderConfirmError", id.ToString(), ex.Message);
                return NotFound();
            }
        }

        //私有方法：加密解密工具
        private string EncryptAES(string plainText, string key, string iv)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = Encoding.UTF8.GetBytes(iv);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            using var encryptor = aes.CreateEncryptor();
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            return Convert.ToHexString(encryptedBytes).ToLower();
        }

        private string DecryptAES(string cipherHex, string key, string iv)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.IV = Encoding.UTF8.GetBytes(iv);
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            var cipherBytes = Convert.FromHexString(cipherHex);
            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }

        private string GenerateCheckValue(string tradeInfo, string key, string iv)
        {
            var checkString = $"HashKey={key}&{tradeInfo}&HashIV={iv}";
            using var sha256 = SHA256.Create();
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(checkString));
            return Convert.ToHexString(hashBytes).ToUpper();
        }

        private string ExtractMerchantOrderNo(Dictionary<string, object> result)
        {
            if (result.ContainsKey("Result") && result["Result"] is JsonElement element &&
                element.TryGetProperty("MerchantOrderNo", out var orderNoElem))
            {
                return orderNoElem.GetString() ?? "";
            }
            return "";
        }

        private string ExtractPaymentReference(Dictionary<string, object> result)
        {
            if (result.ContainsKey("Result") && result["Result"] is JsonElement element &&
                element.TryGetProperty("TradeNo", out var tradeNoElem))
            {
                return tradeNoElem.GetString() ?? "";
            }
            return "";
        }

        private List<PaymentMethodViewModel> GetAvailablePaymentMethods()
        {
            return new List<PaymentMethodViewModel>
            {
                new() { Id = "信用卡", Name = "信用卡", Description = "支援 Visa、MasterCard、JCB", Icon = "💳" },
                new() { Id = "Line Pay", Name = "Line Pay", Description = "使用 Line Pay 快速付款", Icon = "💚" },
                new() { Id = "貨到付款", Name = "貨到付款", Description = "收到商品時再付款", Icon = "📦" }
            };
        }
    }

    //ViewModel 類別
    public class CheckoutViewModel
    {
        public User User { get; set; } = null!;
        public List<CheckoutItemViewModel> Items { get; set; } = new();
        public decimal Subtotal { get; set; }
        public decimal ShippingCost { get; set; }
        public decimal Total { get; set; }
        public List<PaymentMethodViewModel> PaymentMethods { get; set; } = new();
    }
    public class CheckoutItemViewModel
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Subtotal { get; set; }
        public bool HasDiscount { get; set; }
    }
    public class CheckoutSubmitModel
    {
        [Required(ErrorMessage = "請填寫收件地址")]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required(ErrorMessage = "請選擇付款方式")]
        public string PaymentMethod { get; set; } = string.Empty;

        public string? ShippingMethod { get; set; }
        public string? Notes { get; set; }
    }
    public class PaymentMethodViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }
    public class NewebPayViewModel
    {
        public string MerchantID { get; set; } = string.Empty;
        public string TradeInfo { get; set; } = string.Empty;
        public string TradeSha { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public Order Order { get; set; } = null!;
    }
}