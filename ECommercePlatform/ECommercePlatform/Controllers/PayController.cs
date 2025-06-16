using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Services;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Specialized;
using System.Net.Http.Headers;
using System.Text;
using System.Web;

namespace ECommercePlatform.Controllers
{
    public class PayController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IOrderService _orderService;
        private readonly OperationLogService _logService;

        public PayController(
            ApplicationDbContext context,
            IOrderService orderService,
            OperationLogService logService)
        {
            _context = context;
            _orderService = orderService;
            _logService = logService;
        }
        public IConfiguration Config = new ConfigurationBuilder().AddJsonFile("appSettings.json").Build();

        public async void Pay(decimal totalAmount)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    var orderId = Guid.NewGuid().ToString().Replace("-", "").Substring(0, 20);

                    var order = new Dictionary<string, object>
                    {
                        //特店交易編號
                        { "MerchantTradeNo",  orderId},

                        //特店交易時間 yyyy/MM/dd HH:mm:ss
                        { "MerchantTradeDate",  DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss")},

                        //交易金額
                        { "TotalAmount",  totalAmount},

                        //交易描述
                        { "TradeDesc",  "Ez購"},

                        //商品名稱
                        { "ItemName", "測試"},

                        //允許繳費有效天數(付款方式為 ATM 時，需設定此值)
                        { "ExpireDate",  "3"},

                        //自訂名稱欄位1
                        { "Email",  "teamoneproject004@gmail.com"},

                        //自訂名稱欄位2
                        { "CustomField2",  ""},

                        //自訂名稱欄位3
                        { "CustomField3",  ""},

                        //自訂名稱欄位4
                        { "CustomField4",  ""},

                        //完成後發通知
                        { "ReturnURL",  $"{Config.GetSection("HostURL").Value}/Pay/CallbackNotify"},

                        //付款完成後導頁
                        { "OrderResultURL", $"{Config.GetSection("HostURL").Value}/Pay/CallbackReturn"},

                        //付款方式為 ATM 時，當使用者於綠界操作結束時，綠界回傳 虛擬帳號資訊至 此URL
                        { "PaymentInfoURL",$"{Config.GetSection("HostURL").Value}/Pay/CallbackCustomer"},

                        //付款方式為 ATM 時，當使用者於綠界操作結束時，綠界會轉址至 此URL。
                        { "ClientRedirectURL",  $"{Config.GetSection("HostURL").Value}/Pay/CallbackCustomer"},

                        //特店編號， 2000132 測試綠界編號
                        { "MerchantID",  "3002599"},

                        //忽略付款方式
                        { "IgnorePayment",  "GooglePay#WebATM#CVS#BARCODE"},

                        //交易類型 固定填入 aio
                        { "PaymentType",  "aio"},

                        //選擇預設付款方式 固定填入 ALL
                        { "ChoosePayment",  "ALL"},

                        //CheckMacValue 加密類型 固定填入 1 (SHA256)
                        { "EncryptType",  "1"},
                    };

                    //檢查碼
                    order["CheckMacValue"] = GetCheckMacValue(order);

                    StringBuilder s = new StringBuilder();
                    s.Append("{");
                    foreach (KeyValuePair<string, object> item in order)
                    {
                        s.AppendFormat("{0}='{1}',", item.Key, item.Value);
                    }

                    s.Remove(s.Length - 1, 1);
                    s.Append('}');

                    string url = "https://payment-stage.ecpay.com.tw/Cashier/AioCheckOut/V5";
                    string json = s.ToString();
                    HttpContent content = new StringContent(json, Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync(url, content);

                }
            }
            catch (Exception ex)
            {
                _logService.Log("Order", "PayError", "", ex.Message);
            }
        }

        /// <summary>
        /// 取得 檢查碼
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private string GetCheckMacValue(Dictionary<string, object> order)
        {
            var param = order.Keys.OrderBy(x => x).Select(key => key + "=" + order[key]).ToList();

            var checkValue = string.Join("&", param);

            //測試用的 HashKey
            var hashKey = "spPjZn66i0OhqJsQ";

            //測試用的 HashIV
            var HashIV = "hT5OJckN45isQTTs";

            checkValue = $"HashKey={hashKey}" + "&" + checkValue + $"&HashIV={HashIV}";

            checkValue = HttpUtility.UrlEncode(checkValue).ToLower();

            checkValue = EncryptSHA256(checkValue);

            return checkValue.ToUpper();
        }

        /// <summary>
        /// 字串加密SHA256
        /// </summary>
        /// <param name="source">加密前字串</param>
        /// <returns>加密後字串</returns>
        public string EncryptSHA256(string source)
        {
            string result = string.Empty;

            using (System.Security.Cryptography.SHA256 algorithm = System.Security.Cryptography.SHA256.Create())
            {
                var hash = algorithm.ComputeHash(Encoding.UTF8.GetBytes(source));

                if (hash != null)
                {
                    result = BitConverter.ToString(hash)?.Replace("-", string.Empty)?.ToUpper();
                }

            }
            return result;
        }

        /// <summary>
        /// 支付完成返回網址
        /// </summary>
        /// <returns></returns>
        public IActionResult CallbackReturn()
        {
            var result = GetCallbackResult(Request.Form);
            ViewData["ReceiveObj"] = result.ReceiveObj;
            ViewData["TradeInfo"] = result.TradeInfo;

            return View();
        }


        /// <summary>
        /// 商店取號網址
        /// </summary>
        /// <returns></returns>
        public IActionResult CallbackCustomer()
        {
            var result = GetCallbackResult(Request.Form);
            ViewData["ReceiveObj"] = result.ReceiveObj;
            ViewData["TradeInfo"] = result.TradeInfo;
            return View();
        }

        /// <summary>
        /// 支付通知網址
        /// </summary>
        /// <returns></returns>
        public PayResult GetCallbackResult(IFormCollection form)
        {
            // 接收參數
            StringBuilder receive = new StringBuilder();
            foreach (var item in form)
            {
                receive.AppendLine(item.Key + "=" + item.Value + "<br>");
            }
            var result = new PayResult
            {
                ReceiveObj = receive.ToString(),
            };

            // 解密訊息
            IConfiguration Config = new ConfigurationBuilder().AddJsonFile("appSettings.json").Build();
            string HashKey = Config.GetSection("HashKey").Value;//API 串接金鑰
            string HashIV = Config.GetSection("HashIV").Value;//API 串接密碼
            string TradeInfoDecrypt = DecryptAESHex(form["TradeInfo"], HashKey, HashIV);
            NameValueCollection decryptTradeCollection = HttpUtility.ParseQueryString(TradeInfoDecrypt);
            receive.Length = 0;
            foreach (String key in decryptTradeCollection.AllKeys)
            {
                receive.AppendLine(key + "=" + decryptTradeCollection[key] + "<br>");
            }
            result.TradeInfo = receive.ToString();

            return result;
        }

        /// <summary>
        /// 16 進制字串解密
        /// </summary>
        /// <param name="source">加密前字串</param>
        /// <param name="cryptoKey">加密金鑰</param>
        /// <param name="cryptoIV">cryptoIV</param>
        /// <returns>解密後的字串</returns>
        public string DecryptAESHex(string source, string cryptoKey, string cryptoIV)
        {
            string result = string.Empty;

            if (!string.IsNullOrEmpty(source))
            {
                // 將 16 進制字串 轉為 byte[] 後
                byte[] sourceBytes = ToByteArray(source);

                if (sourceBytes != null)
                {
                    // 使用金鑰解密後，轉回 加密前 value
                    result = Encoding.UTF8.GetString(DecryptAES(sourceBytes, cryptoKey, cryptoIV)).Trim();
                }
            }

            return result;
        }

        /// <summary>
        /// 將16進位字串轉換為byteArray
        /// </summary>
        /// <param name="source">欲轉換之字串</param>
        /// <returns></returns>
        public byte[] ToByteArray(string source)
        {
            byte[] result = null;

            if (!string.IsNullOrWhiteSpace(source))
            {
                var outputLength = source.Length / 2;
                var output = new byte[outputLength];

                for (var i = 0; i < outputLength; i++)
                {
                    output[i] = Convert.ToByte(source.Substring(i * 2, 2), 16);
                }
                result = output;
            }

            return result;
        }

        /// <summary>
        /// 字串解密AES
        /// </summary>
        /// <param name="source">解密前字串</param>
        /// <param name="cryptoKey">解密金鑰</param>
        /// <param name="cryptoIV">cryptoIV</param>
        /// <returns>解密後字串</returns>
        public byte[] DecryptAES(byte[] source, string cryptoKey, string cryptoIV)
        {
            byte[] dataKey = Encoding.UTF8.GetBytes(cryptoKey);
            byte[] dataIV = Encoding.UTF8.GetBytes(cryptoIV);

            using (var aes = System.Security.Cryptography.Aes.Create())
            {
                aes.Mode = System.Security.Cryptography.CipherMode.CBC;
                // 智付通無法直接用PaddingMode.PKCS7，會跳"填補無效，而且無法移除。"
                // 所以改為PaddingMode.None並搭配RemovePKCS7Padding
                aes.Padding = System.Security.Cryptography.PaddingMode.None;
                aes.Key = dataKey;
                aes.IV = dataIV;

                using (var decryptor = aes.CreateDecryptor())
                {
                    byte[] data = decryptor.TransformFinalBlock(source, 0, source.Length);
                    int iLength = data[data.Length - 1];
                    var output = new byte[data.Length - iLength];
                    Buffer.BlockCopy(data, 0, output, 0, output.Length);
                    return output;
                }
            }
        }

        /// <summary>
        /// 支付通知網址
        /// </summary>
        /// <returns></returns>
        public HttpResponseMessage CallbackNotify()
        {
            var result = GetCallbackResult(Request.Form);

            //TODO 支付成功後 可做後續訂單處理

            return ResponseOK();
        }

        /// <summary>
        /// 回傳給 綠界 失敗
        /// </summary>
        /// <returns></returns>
        private HttpResponseMessage ResponseError()
        {
            var response = new HttpResponseMessage();
            response.Content = new StringContent("0|Error");
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }

        /// <summary>
        /// 回傳給 綠界 成功
        /// </summary>
        /// <returns></returns>
        private HttpResponseMessage ResponseOK()
        {
            var response = new HttpResponseMessage();
            response.Content = new StringContent("1|OK");
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/html");
            return response;
        }
    }
}
