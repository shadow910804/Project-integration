using Microsoft.EntityFrameworkCore;
using ECommercePlatform.Models;

namespace ECommercePlatform.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }
        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<DeviceToken> DeviceTokens { get; set; }
        public DbSet<Engineer>? Engineers { get; set; }
        public DbSet<OperationLog>? OperationLogs { get; set; }
        public DbSet<ReviewReport> ReviewReports { get; set; }

        // 新增：庫存管理相關實體
        public DbSet<StockReservation> StockReservations { get; set; }
        public DbSet<StockMovement> StockMovements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // User 配置
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Role).HasDefaultValue("User");
                entity.Property(e => e.IsActive).HasDefaultValue(true);
            });

            // Product 配置
            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.DiscountPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Stock).HasDefaultValue(0); //庫存預設值

                // 添加庫存檢查約束 (使用新的語法)
                entity.ToTable(t => t.HasCheckConstraint("CK_Product_Stock", "Stock >= 0"));
            });

            // Order 配置
            modelBuilder.Entity<Order>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalAmount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.OrderDate).HasDefaultValueSql("GETUTCDATE()");

                entity.HasOne(e => e.User)
                      .WithMany(u => u.Orders)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // OrderItem 配置
            modelBuilder.Entity<OrderItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");

                entity.HasOne(e => e.Order)
                      .WithMany(o => o.OrderItems)
                      .HasForeignKey(e => e.OrderId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.OrderItems)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // CartItem 配置
            modelBuilder.Entity<CartItem>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.User)
                      .WithMany(u => u.CartItems)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.CartItems)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.ProductId }).IsUnique();
            });

            // Review 配置
            modelBuilder.Entity<Review>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("Reviews");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsVisible).HasDefaultValue(true);

                entity.HasOne(e => e.User)
                      .WithMany(u => u.Reviews)
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Product)
                      .WithMany(p => p.Reviews)
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.ReplyTo)
                      .WithMany(r => r.Replies)
                      .HasForeignKey(e => e.ReplyId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => new { e.UserId, e.ProductId });
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.Rating);
                entity.HasIndex(e => e.IsVisible);
            });

            // ReviewReport 配置
            modelBuilder.Entity<ReviewReport>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.ToTable("ReviewReports");

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsProcessed).HasDefaultValue(false);

                entity.HasOne(e => e.Review)
                      .WithMany(r => r.Reports)
                      .HasForeignKey(e => e.ReviewId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Reporter)
                      .WithMany()
                      .HasForeignKey(e => e.ReporterId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.IsProcessed);
                entity.HasIndex(e => e.CreatedAt);
            });

            // Engineer 配置
            modelBuilder.Entity<Engineer>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Username).IsUnique();
                entity.HasIndex(e => e.Email).IsUnique();
            });

            // OperationLog 配置
            modelBuilder.Entity<OperationLog>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();

                entity.HasOne(e => e.Engineer)
                      .WithMany(eng => eng.OperationLogs)
                      .HasForeignKey(e => e.EngineerId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.Property(e => e.ActionTime).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Timestamp).HasDefaultValueSql("GETUTCDATE()");

                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.ActionTime);
                entity.HasIndex(e => e.Controller);
            });

            // DeviceToken 配置
            modelBuilder.Entity<DeviceToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            });

            //StockReservation 配置
            modelBuilder.Entity<StockReservation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).HasMaxLength(50); // GUID 字串

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.IsConfirmed).HasDefaultValue(false);

                // 索引優化
                entity.HasIndex(e => new { e.ProductId, e.ExpiresAt, e.IsConfirmed });
                entity.HasIndex(e => e.ExpiresAt);

                // 檢查約束 (使用新的語法)
                entity.ToTable(t => t.HasCheckConstraint("CK_StockReservation_Quantity", "Quantity > 0"));
            });

            // 新增：StockMovement 配置
            modelBuilder.Entity<StockMovement>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.HasOne(e => e.Product)
                      .WithMany()
                      .HasForeignKey(e => e.ProductId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.User)
                      .WithMany()
                      .HasForeignKey(e => e.UserId)
                      .OnDelete(DeleteBehavior.SetNull)
                      .IsRequired(false);

                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
                entity.Property(e => e.Reason).HasMaxLength(500);

                // 索引優化
                entity.HasIndex(e => new { e.ProductId, e.CreatedAt });
                entity.HasIndex(e => e.MovementType);
                entity.HasIndex(e => e.CreatedAt);

                // 枚舉轉換
                entity.Property(e => e.MovementType)
                      .HasConversion<string>();
            });
        }

        // 新增：資料庫初始化時的額外配置
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // 自動設定 UpdatedAt 時間戳
            var entries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Modified)
                .ToList();

            foreach (var entry in entries)
            {
                if (entry.Entity is CartItem cartItem)
                {
                    cartItem.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is Review review && review.UpdatedAt == null)
                {
                    review.UpdatedAt = DateTime.UtcNow;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }
    }
}