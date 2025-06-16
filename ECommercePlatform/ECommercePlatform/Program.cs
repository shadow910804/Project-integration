using ECommercePlatform.Data;
using ECommercePlatform.Models;
using ECommercePlatform.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 資料庫連線設定
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// MVC 與 SignalR
builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// 正確註冊 Cookie Schemes（只註冊一次每個名稱）
builder.Services.AddAuthentication()
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme,options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(24); // 延長到24小時
    });

// 註冊核心服務
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<OperationLogService>();

// 註冊新的業務服務
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// 註冊後台任務服務
builder.Services.AddHostedService<InventoryCleanupService>();

// Authorization
builder.Services.AddAuthorization();

// 支援 Operation Log 與 HttpContext
builder.Services.AddHttpContextAccessor();

// 添加 CORS 支援（如果需要 API 調用）
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// 添加這個重要的設定 - 顯示詳細錯誤訊息
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();

app.UseRouting();

// 啟用 CORS
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

// 在 Program.cs 中的資料庫初始化部分（替換原有的部分）

// 資料庫初始化和遷移
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("開始資料庫初始化...");

        // 確保資料庫存在並執行遷移
        await context.Database.EnsureCreatedAsync();

        // 檢查並執行待處理的遷移
        var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
        if (pendingMigrations.Any())
        {
            logger.LogInformation($"正在執行 {pendingMigrations.Count()} 個待處理的遷移...");
            await context.Database.MigrateAsync();
            logger.LogInformation("資料庫遷移完成");
        }

        // 初始化種子資料
        logger.LogInformation("開始初始化種子資料...");
        DbInitializer.Seed(context);

        // 檢查資料庫狀態
        DbInitializer.CheckDatabaseStatus(context);

        logger.LogInformation("種子資料初始化完成");

        // 額外檢查評價資料
        var reviewCount = await context.Reviews.CountAsync();
        var visibleReviewCount = await context.Reviews.CountAsync(r => r.IsVisible);

        logger.LogInformation($"評價資料檢查：總計 {reviewCount} 則，可見 {visibleReviewCount} 則");

        if (reviewCount == 0)
        {
            logger.LogWarning("⚠️ 沒有發現評價資料，請檢查種子資料初始化");

            // 嘗試手動重新初始化評價資料
            try
            {
                logger.LogInformation("嘗試手動重新初始化評價資料...");

                var users = await context.Users.ToListAsync();
                var products = await context.Products.ToListAsync();

                if (users.Any() && products.Any())
                {
                    var john = users.FirstOrDefault(u => u.Username == "johndoe");
                    var jane = users.FirstOrDefault(u => u.Username == "janedoe");
                    var admin = users.FirstOrDefault(u => u.Username == "admin");

                    var iphone = products.FirstOrDefault(p => p.Name.Contains("iPhone"));
                    var macbook = products.FirstOrDefault(p => p.Name.Contains("MacBook"));
                    var airpods = products.FirstOrDefault(p => p.Name.Contains("AirPods"));

                    if (john != null && jane != null && admin != null &&
                        iphone != null && macbook != null && airpods != null)
                    {
                        var reviews = new[]
                        {
                            new Review
                            {
                                UserId = john.Id,
                                ProductId = iphone.Id,
                                UserName = john.Username,
                                Content = "iPhone 15 Pro 真的很棒！相機品質提升很多，鈦金屬設計很有質感。",
                                Rating = 5,
                                CreatedAt = DateTime.UtcNow.AddDays(-10),
                                IsVisible = true
                            },
                            new Review
                            {
                                UserId = jane.Id,
                                ProductId = iphone.Id,
                                UserName = jane.Username,
                                Content = "價格有點高，但是性能確實很好，建議等特價再買。",
                                Rating = 4,
                                CreatedAt = DateTime.UtcNow.AddDays(-8),
                                IsVisible = true
                            },
                            new Review
                            {
                                UserId = admin.Id,
                                ProductId = macbook.Id,
                                UserName = admin.Username,
                                Content = "M3 晶片的效能真的很驚人，編譯速度快很多！",
                                Rating = 5,
                                CreatedAt = DateTime.UtcNow.AddDays(-5),
                                IsVisible = true
                            },
                            new Review
                            {
                                UserId = jane.Id,
                                ProductId = airpods.Id,
                                UserName = jane.Username,
                                Content = "降噪效果很棒，音質也很好，值得購買！",
                                Rating = 5,
                                CreatedAt = DateTime.UtcNow.AddDays(-3),
                                IsVisible = true
                            },
                            new Review
                            {
                                UserId = john.Id,
                                ProductId = airpods.Id,
                                UserName = john.Username,
                                Content = "整體不錯，但是價格還是偏高。",
                                Rating = 4,
                                CreatedAt = DateTime.UtcNow.AddDays(-1),
                                IsVisible = true
                            }
                        };

                        context.Reviews.AddRange(reviews);
                        await context.SaveChangesAsync();

                        logger.LogInformation($"✅ 成功手動創建 {reviews.Length} 則評價資料");
                    }
                    else
                    {
                        logger.LogWarning("找不到必要的用戶或商品資料");
                    }
                }
                else
                {
                    logger.LogWarning("缺少用戶或商品資料，無法創建評價");
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "手動初始化評價資料失敗");
            }
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "資料庫初始化失敗");

        // 在開發環境中拋出異常，生產環境中記錄錯誤但繼續運行
        if (app.Environment.IsDevelopment())
        {
            throw;
        }
    }
}

// 添加 API 路由配置
app.MapControllerRoute(
    name: "api",
    pattern: "api/{controller}/{action=Index}/{id?}");

// 預設路由
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 添加健康檢查端點
app.MapGet("/health", async (ApplicationDbContext context) =>
{
    try
    {
        await context.Database.CanConnectAsync();
        return Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database connection failed",
            detail: ex.Message,
            statusCode: 503);
    }
});

app.Run();

//後台服務：清理過期庫存預留
public class InventoryCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<InventoryCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5); // 每5分鐘執行一次

    public InventoryCleanupService(IServiceProvider serviceProvider, ILogger<InventoryCleanupService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var inventoryService = scope.ServiceProvider.GetRequiredService<IInventoryService>();

                await inventoryService.CleanupExpiredReservationsAsync();

                _logger.LogDebug("庫存預留清理任務完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "庫存預留清理任務執行失敗");
            }

            await Task.Delay(_cleanupInterval, stoppingToken);
        }
    }
}

//擴展的資料庫初始化器
public static class EnhancedDbInitializer
{
    public static void SeedEnhancedData(ApplicationDbContext context)
    {
        // 確保基礎資料存在
        DbInitializer.Seed(context);

        // 添加庫存變動歷史的種子資料
        if (!context.StockMovements.Any() && context.Products.Any())
        {
            var products = context.Products.Take(3).ToList();
            var admin = context.Users.FirstOrDefault(u => u.Role == "Admin");

            var movements = new List<StockMovement>();

            foreach (var product in products)
            {
                // 初始進貨記錄
                movements.Add(new StockMovement
                {
                    ProductId = product.Id,
                    MovementType = StockMovementType.Adjustment_In,
                    Quantity = product.Stock,
                    PreviousStock = 0,
                    NewStock = product.Stock,
                    Reason = "初始進貨",
                    UserId = admin?.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-30)
                });
            }

            context.StockMovements.AddRange(movements);
            context.SaveChanges();
        }

        // 添加測試用的操作日誌
        if (context.OperationLogs != null && !context.OperationLogs.Any())
        {
            var engineer = context.Engineers?.FirstOrDefault();
            if (engineer != null)
            {
                var logs = new List<OperationLog>
                {
                    new OperationLog
                    {
                        EngineerId = engineer.Id,
                        Controller = "System",
                        Action = "Initialize",
                        Description = "系統初始化完成",
                        ActionTime = DateTime.UtcNow,
                        Timestamp = DateTime.UtcNow
                    }
                };

                context.OperationLogs.AddRange(logs);
                context.SaveChanges();
            }
        }
    }
}

//全域異常處理中介軟體
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "未處理的異常發生在 {Path}", context.Request.Path);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var response = new
        {
            error = "系統發生錯誤，請稍後再試",
            timestamp = DateTime.UtcNow,
            path = context.Request.Path.Value
        };

        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
}

// 使用中介軟體的擴展方法
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
