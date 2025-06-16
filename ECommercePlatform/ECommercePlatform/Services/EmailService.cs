using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using ECommercePlatform.Models;

namespace ECommercePlatform.Services
{
    public class EmailService
    {
        private readonly string _emailAccount;
        private readonly string _emailPassword;
        private readonly string _smtpServer;
        private readonly int _smtpPort;

        public EmailService(IConfiguration configuration)
        {
            var emailSettings = configuration.GetSection("EmailSettings");
            _emailAccount = emailSettings["SmtpUsername"] ??
                           Environment.GetEnvironmentVariable("EMAIL_ACCOUNT") ??
                           throw new Exception("Missing email account configuration");
            _emailPassword = emailSettings["SmtpPassword"] ??
                            Environment.GetEnvironmentVariable("EMAIL_PASSWORD") ??
                            throw new Exception("Missing email password configuration");
            _smtpServer = emailSettings["SmtpServer"] ?? "smtp.gmail.com";
            _smtpPort = int.Parse(emailSettings["SmtpPort"] ?? "587");
        }

        // 發送檢舉郵件（保留原有功能）
        public async Task SendComplainMailAsync(string subject, string body, string to = "testproject9487@gmail.com")
        {
            await SendEmailAsync(to, subject, body);
        }

        // 同步版本（向後相容）
        public void SendComplainMail(string subject, string body, string to = "testproject9487@gmail.com")
        {
            SendComplainMailAsync(subject, body, to).GetAwaiter().GetResult();
        }

        // 發送歡迎郵件
        public async Task SendWelcomeEmailAsync(string to, string username)
        {
            var subject = "歡迎加入 Ez購,Ez Life！";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 20px; text-align: center;'>
                        <h1>🛒 Ez購,Ez Life</h1>
                        <h2>歡迎加入我們！</h2>
                    </div>
                    <div style='padding: 20px; background: #f9f9f9;'>
                        <p>親愛的 <strong>{username}</strong>，</p>
                        <p>感謝您註冊成為我們的會員！現在您可以享受以下服務：</p>
                        <ul>
                            <li>🛍️ 瀏覽精選商品</li>
                            <li>🛒 輕鬆購物體驗</li>
                            <li>💬 商品評價與分享</li>
                            <li>📦 訂單追蹤管理</li>
                            <li>🎁 專屬會員優惠</li>
                        </ul>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='#' style='background: #667eea; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                🚀 開始購物
                            </a>
                        </div>
                        <p>如有任何問題，請隨時聯繫我們的客服團隊。</p>
                        <p>祝您購物愉快！<br>Ez購,Ez Life 團隊</p>
                    </div>
                    <div style='background: #333; color: white; padding: 10px; text-align: center; font-size: 12px;'>
                        © 2025 Ez購,Ez Life. All rights reserved.
                    </div>
                </div>";

            await SendEmailAsync(to, subject, body);
        }

        // 發送訂單確認郵件
        public async Task SendOrderConfirmationAsync(string to, Order order, string customerName)
        {
            var subject = $"訂單確認 - #{order.Id}";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: #28a745; color: white; padding: 20px; text-align: center;'>
                        <h1>📦 訂單確認</h1>
                        <h2>訂單編號：#{order.Id}</h2>
                    </div>
                    <div style='padding: 20px; background: #f9f9f9;'>
                        <p>親愛的 <strong>{customerName}</strong>，</p>
                        <p>感謝您的訂購！您的訂單已成功建立，詳細資訊如下：</p>
                        
                        <div style='background: white; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <h3>📋 訂單資訊</h3>
                            <p><strong>訂單編號：</strong>{order.Id}</p>
                            <p><strong>訂購時間：</strong>{order.OrderDate:yyyy年MM月dd日 HH:mm}</p>
                            <p><strong>訂單狀態：</strong>{order.OrderStatus}</p>
                            <p><strong>付款方式：</strong>{order.PaymentMethod}</p>
                            <p><strong>收貨地址：</strong>{order.ShippingAddress}</p>
                            <p><strong>訂單總額：</strong><span style='color: #28a745; font-size: 18px; font-weight: bold;'>NT$ {order.TotalAmount:N0}</span></p>
                        </div>

                        <div style='background: white; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <h3>📝 處理流程</h3>
                            <ol>
                                <li>✅ 訂單已確認</li>
                                <li>⏳ 準備出貨中</li>
                                <li>🚚 商品配送中</li>
                                <li>📦 送達完成</li>
                            </ol>
                        </div>

                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='#' style='background: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 5px;'>
                                📋 查看訂單
                            </a>
                            <a href='#' style='background: #28a745; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin: 5px;'>
                                🛍️ 繼續購物
                            </a>
                        </div>

                        <p>我們會盡快為您處理訂單，並在每個階段向您發送更新通知。</p>
                        <p>如有任何問題，請聯繫客服團隊。</p>
                        <p>謝謝您的支持！<br>Ez購,Ez Life 團隊</p>
                    </div>
                </div>";

            await SendEmailAsync(to, subject, body);
        }

        // 發送訂單狀態更新郵件
        public async Task SendOrderStatusUpdateAsync(string to, Order order, string customerName, string newStatus)
        {
            var statusMessages = new Dictionary<string, (string Icon, string Message, string Color)>
            {
                ["待處理"] = ("⏳", "我們已收到您的訂單，正在準備處理中", "#ffc107"),
                ["處理中"] = ("🔄", "您的訂單正在處理中，我們正在為您準備商品", "#17a2b8"),
                ["已發貨"] = ("🚚", "好消息！您的訂單已發貨，正在配送途中", "#28a745"),
                ["已送達"] = ("📦", "您的訂單已成功送達，感謝您的購買！", "#28a745"),
                ["已取消"] = ("❌", "您的訂單已取消，如有疑問請聯繫客服", "#dc3545")
            };

            var (icon, message, color) = statusMessages.GetValueOrDefault(newStatus, ("📋", "訂單狀態已更新", "#6c757d"));

            var subject = $"訂單狀態更新 - #{order.Id}";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: {color}; color: white; padding: 20px; text-align: center;'>
                        <h1>{icon} 訂單狀態更新</h1>
                        <h2>訂單編號：#{order.Id}</h2>
                    </div>
                    <div style='padding: 20px; background: #f9f9f9;'>
                        <p>親愛的 <strong>{customerName}</strong>，</p>
                        <div style='background: white; padding: 20px; border-radius: 5px; border-left: 4px solid {color}; margin: 20px 0;'>
                            <h3 style='color: {color}; margin-top: 0;'>狀態：{newStatus}</h3>
                            <p style='font-size: 16px;'>{message}</p>
                        </div>
                        <p>訂單總額：<strong>NT$ {order.TotalAmount:N0}</strong></p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='#' style='background: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                📋 查看訂單詳情
                            </a>
                        </div>
                        <p>感謝您選擇 Ez購,Ez Life！</p>
                    </div>
                </div>";

            await SendEmailAsync(to, subject, body);
        }

        // 發送密碼重設郵件
        public async Task SendPasswordResetAsync(string to, string username, string resetToken)
        {
            var resetUrl = $"https://your-domain.com/Account/ResetPassword?token={resetToken}";
            var subject = "密碼重設請求";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: #dc3545; color: white; padding: 20px; text-align: center;'>
                        <h1>🔒 密碼重設</h1>
                    </div>
                    <div style='padding: 20px; background: #f9f9f9;'>
                        <p>親愛的 <strong>{username}</strong>，</p>
                        <p>我們收到了您的密碼重設請求。如果這是您本人的操作，請點擊下方按鈕重設密碼：</p>
                        <div style='text-align: center; margin: 30px 0;'>
                            <a href='{resetUrl}' style='background: #dc3545; color: white; padding: 15px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                🔒 重設密碼
                            </a>
                        </div>
                        <p style='color: #666; font-size: 14px;'>此連結將在 24 小時後失效。</p>
                        <p style='color: #666; font-size: 14px;'>如果您沒有請求重設密碼，請忽略此郵件。</p>
                        <div style='background: #fff3cd; border: 1px solid #ffeaa7; padding: 15px; border-radius: 5px; margin: 20px 0;'>
                            <p style='margin: 0; color: #856404;'><strong>安全提醒：</strong>請勿將此連結分享給他人，並確保在受信任的設備上重設密碼。</p>
                        </div>
                    </div>
                </div>";

            await SendEmailAsync(to, subject, body);
        }

        // 發送庫存不足通知郵件（給管理員）
        public async Task SendLowStockAlertAsync(string productName, int currentStock, int threshold = 5)
        {
            var adminEmail = "admin@your-domain.com"; // 可配置
            var subject = $"庫存不足警告 - {productName}";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <div style='background: #ffc107; color: #212529; padding: 20px; text-align: center;'>
                        <h1>⚠️ 庫存不足警告</h1>
                    </div>
                    <div style='padding: 20px; background: #f9f9f9;'>
                        <h3>商品庫存即將用盡</h3>
                        <div style='background: white; padding: 15px; border-radius: 5px; border-left: 4px solid #ffc107;'>
                            <p><strong>商品名稱：</strong>{productName}</p>
                            <p><strong>當前庫存：</strong><span style='color: #dc3545; font-weight: bold;'>{currentStock} 件</span></p>
                            <p><strong>警告閾值：</strong>{threshold} 件</p>
                        </div>
                        <p>建議儘快補充庫存，以避免影響銷售。</p>
                        <div style='text-align: center; margin: 20px 0;'>
                            <a href='#' style='background: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block;'>
                                📦 管理庫存
                            </a>
                        </div>
                    </div>
                </div>";

            await SendEmailAsync(adminEmail, subject, body);
        }


        // 通用郵件發送方法
        private async Task SendEmailAsync(string to, string subject, string htmlBody)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Ez購,Ez Life", _emailAccount));
                message.To.Add(MailboxAddress.Parse(to));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder
                {
                    HtmlBody = htmlBody
                };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                await client.ConnectAsync(_smtpServer, _smtpPort, SecureSocketOptions.StartTls);
                await client.AuthenticateAsync(_emailAccount, _emailPassword);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                // 記錄錯誤但不拋出異常，避免影響主要業務流程
                Console.WriteLine($"Email sending failed: {ex.Message}");
                throw; // 在開發環境可以拋出，生產環境可能需要靜默處理
            }
        }
    }
}