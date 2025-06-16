using System;
using ECommercePlatform.Data;
using ECommercePlatform.Models;

public static class DbInitializer
{
    public static void Seed(ApplicationDbContext context)
    {
        // 確保資料庫已創建
        context.Database.EnsureCreated();

        // 添加用戶資料
        if (!context.Users.Any())
        {
            var users = new[]
            {
                new User
                {
                    Username = "admin",
                    Email = "admin@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
                    Role = "Admin",
                    Address = "123 Admin St",
                    PhoneNumber = "0987654321",
                    FirstName = "Admin",
                    LastName = "User",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                new User
                {
                    Username = "johndoe",
                    Email = "john@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                    Role = "User",
                    Address = "123 Main St",
                    PhoneNumber = "0987654321",
                    FirstName = "John",
                    LastName = "Doe",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                },
                new User
                {
                    Username = "janedoe",
                    Email = "jane@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("password456"),
                    Role = "User",
                    Address = "456 Second St",
                    PhoneNumber = "0912345678",
                    FirstName = "Jane",
                    LastName = "Doe",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                }
            };

            context.Users.AddRange(users);
            context.SaveChanges();
            Console.WriteLine($"✅ 已新增 {users.Length} 個用戶");
        }

        // 添加商品資料
        if (!context.Products.Any())
        {
            var products = new[]
            {
                new Product
                {
                    Name = "iPhone 15 Pro",
                    Description = "最新款 iPhone，配備 A17 Pro 晶片，鈦金屬設計。",
                    Price = 36900m,
                    DiscountPrice = 34900m,
                    DiscountStart = DateTime.UtcNow,
                    DiscountEnd = DateTime.UtcNow.AddDays(30),
                    ImageUrl = "/images/iphone15pro.jpg",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Stock = 50
                },
                new Product
                {
                    Name = "MacBook Air M3",
                    Description = "輕薄強效的 MacBook Air，搭載 M3 晶片。",
                    Price = 41900m,
                    ImageUrl = "/images/macbook-air-m3.jpg",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Stock = 30
                },
                new Product
                {
                    Name = "AirPods Pro 2",
                    Description = "主動降噪無線耳機，全新 H2 晶片。",
                    Price = 7490m,
                    DiscountPrice = 6990m,
                    DiscountStart = DateTime.UtcNow,
                    DiscountEnd = DateTime.UtcNow.AddDays(15),
                    ImageUrl = "/images/airpods-pro-2.jpg",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Stock = 100
                },
                new Product
                {
                    Name = "iPad Air",
                    Description = "功能強大的 iPad Air，配備 M1 晶片。",
                    Price = 18900m,
                    ImageUrl = "/images/ipad-air.jpg",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Stock = 25
                },
                new Product
                {
                    Name = "Apple Watch Series 9",
                    Description = "最先進的 Apple Watch，內建 S9 晶片。",
                    Price = 12900m,
                    DiscountPrice = 11900m,
                    DiscountStart = DateTime.UtcNow,
                    DiscountEnd = DateTime.UtcNow.AddDays(20),
                    ImageUrl = "/images/apple-watch-9.jpg",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Stock = 75
                },
                new Product
                {
                    Name = "Magic Keyboard",
                    Description = "適用於 iPad 的 Magic Keyboard，提供絕佳打字體驗。",
                    Price = 10900m,
                    ImageUrl = "/images/magic-keyboard.jpg",
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true,
                    Stock = 15
                }
            };

            context.Products.AddRange(products);
            context.SaveChanges();
            Console.WriteLine($"✅ 已新增 {products.Length} 個商品");
        }

        // 添加工程師資料
        if (context.Engineers != null && !context.Engineers.Any())
        {
            var engineers = new[]
            {
                new Engineer
                {
                    Username = "engineer1",
                    Email = "engineer1@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("engineer123")
                },
                new Engineer
                {
                    Username = "engineer2",
                    Email = "engineer2@example.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("engineer456")
                }
            };

            context.Engineers.AddRange(engineers);
            context.SaveChanges();
            Console.WriteLine($"✅ 已新增 {engineers.Length} 個工程師帳號");
        }

        // 🔥 重點：添加示例評價資料（確保正確執行）
        SeedReviewData(context);
    }

    // 專門的評價資料種子方法
    private static void SeedReviewData(ApplicationDbContext context)
    {
        try
        {
            // 檢查是否已有評價資料
            if (context.Reviews.Any())
            {
                Console.WriteLine("⚠️ 評價資料已存在，跳過初始化");
                return;
            }

            // 確保用戶和商品資料存在
            var users = context.Users.ToList();
            var products = context.Products.ToList();

            if (!users.Any() || !products.Any())
            {
                Console.WriteLine("❌ 缺少用戶或商品資料，無法創建評價");
                return;
            }

            // 找到特定商品
            var iphone = products.FirstOrDefault(p => p.Name.Contains("iPhone"));
            var macbook = products.FirstOrDefault(p => p.Name.Contains("MacBook"));
            var airpods = products.FirstOrDefault(p => p.Name.Contains("AirPods"));
            var ipad = products.FirstOrDefault(p => p.Name.Contains("iPad"));

            // 找到特定用戶
            var john = users.FirstOrDefault(u => u.Username == "johndoe");
            var jane = users.FirstOrDefault(u => u.Username == "janedoe");
            var admin = users.FirstOrDefault(u => u.Username == "admin");

            if (iphone == null || macbook == null || airpods == null ||
                john == null || jane == null || admin == null)
            {
                Console.WriteLine("❌ 找不到必要的用戶或商品資料");
                return;
            }

            var reviews = new[]
            {
                // iPhone 評價
                new Review
                {
                    UserId = john.Id,
                    ProductId = iphone.Id,
                    UserName = john.Username,
                    Content = "iPhone 15 Pro 真的很棒！相機品質提升很多，鈦金屬設計很有質感。攝影效果超乎預期，值得購買！",
                    Rating = 5,
                    CreatedAt = DateTime.UtcNow.AddDays(-10),
                    IsVisible = true
                },
                new Review
                {
                    UserId = jane.Id,
                    ProductId = iphone.Id,
                    UserName = jane.Username,
                    Content = "價格有點高，但是性能確實很好。電池續航比上一代有明顯提升，整體滿意。",
                    Rating = 4,
                    CreatedAt = DateTime.UtcNow.AddDays(-8),
                    IsVisible = true
                },
                new Review
                {
                    UserId = admin.Id,
                    ProductId = iphone.Id,
                    UserName = admin.Username,
                    Content = "作為管理員測試使用，各項功能都很穩定，推薦給需要高性能手機的用戶。",
                    Rating = 5,
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    IsVisible = true
                },

                // MacBook 評價
                new Review
                {
                    UserId = admin.Id,
                    ProductId = macbook.Id,
                    UserName = admin.Username,
                    Content = "M3 晶片的效能真的很驚人，編譯速度快很多！辦公和開發都很順暢。",
                    Rating = 5,
                    CreatedAt = DateTime.UtcNow.AddDays(-7),
                    IsVisible = true
                },
                new Review
                {
                    UserId = john.Id,
                    ProductId = macbook.Id,
                    UserName = john.Username,
                    Content = "輕薄便攜，性能強勁。唯一缺點是價格偏高，但物有所值。",
                    Rating = 4,
                    CreatedAt = DateTime.UtcNow.AddDays(-4),
                    IsVisible = true
                },

                // AirPods 評價
                new Review
                {
                    UserId = jane.Id,
                    ProductId = airpods.Id,
                    UserName = jane.Username,
                    Content = "降噪效果很棒，音質也很好，長時間佩戴也很舒適，值得購買！",
                    Rating = 5,
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    IsVisible = true
                },
                new Review
                {
                    UserId = john.Id,
                    ProductId = airpods.Id,
                    UserName = john.Username,
                    Content = "整體不錯，音質有提升，但是價格還是偏高。",
                    Rating = 4,
                    CreatedAt = DateTime.UtcNow.AddDays(-1),
                    IsVisible = true
                },
                new Review
                {
                    UserId = admin.Id,
                    ProductId = airpods.Id,
                    UserName = admin.Username,
                    Content = "主動降噪功能很實用，適合通勤使用。電池續航也很不錯。",
                    Rating = 5,
                    CreatedAt = DateTime.UtcNow.AddHours(-12),
                    IsVisible = true
                }
            };

            // 如果有 iPad，也加入評價
            if (ipad != null)
            {
                var iPadReviews = new[]
                {
                    new Review
                    {
                        UserId = jane.Id,
                        ProductId = ipad.Id,
                        UserName = jane.Username,
                        Content = "iPad Air 很適合繪圖和筆記，M1 晶片運行很流暢。",
                        Rating = 5,
                        CreatedAt = DateTime.UtcNow.AddDays(-2),
                        IsVisible = true
                    },
                    new Review
                    {
                        UserId = john.Id,
                        ProductId = ipad.Id,
                        UserName = john.Username,
                        Content = "螢幕顯示效果很好，但覺得配件有點貴。",
                        Rating = 4,
                        CreatedAt = DateTime.UtcNow.AddHours(-6),
                        IsVisible = true
                    }
                };

                reviews = reviews.Concat(iPadReviews).ToArray();
            }

            context.Reviews.AddRange(reviews);
            context.SaveChanges();

            Console.WriteLine($"✅ 已新增 {reviews.Length} 則評價資料");

            // 驗證資料是否正確插入
            var insertedReviews = context.Reviews.Count();
            Console.WriteLine($"📊 資料庫中共有 {insertedReviews} 則評價");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 評價資料初始化失敗: {ex.Message}");
            Console.WriteLine($"詳細錯誤: {ex.StackTrace}");
        }
    }

    // 檢查資料庫狀態的輔助方法
    public static void CheckDatabaseStatus(ApplicationDbContext context)
    {
        try
        {
            var userCount = context.Users.Count();
            var productCount = context.Products.Count();
            var reviewCount = context.Reviews.Count();

            Console.WriteLine("=== 資料庫狀態檢查 ===");
            Console.WriteLine($"👥 用戶數量: {userCount}");
            Console.WriteLine($"📱 商品數量: {productCount}");
            Console.WriteLine($"💬 評價數量: {reviewCount}");

            if (reviewCount == 0)
            {
                Console.WriteLine("⚠️ 警告：沒有評價資料，可能需要重新執行種子資料");
            }
            else
            {
                var visibleReviews = context.Reviews.Count(r => r.IsVisible);
                Console.WriteLine($"👁️ 可見評價: {visibleReviews}");
            }

            Console.WriteLine("======================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ 資料庫狀態檢查失敗: {ex.Message}");
        }
    }
}