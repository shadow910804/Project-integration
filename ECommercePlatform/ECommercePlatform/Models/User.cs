using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "使用者名稱是必填的。")]
        public string Username { get; set; } =null!;

        [Required(ErrorMessage = "電子郵件是必填的。")]
        [EmailAddress(ErrorMessage = "電子郵件格式不正確。")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "密碼是必填的。")]
        [DataType(DataType.Password)]
        public string PasswordHash { get; set; } = null!;
        //public string? Role { get; set; }
        public string? Address { get; set; }
        public string? PhoneNumber { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; }

        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        //public ICollection<Review> Reviews { get; set; } = new List<Review>();
        [StringLength(20)]
        public string Role { get; set; } = "User";

        public DateTime? LastLoginAt { get; set; }

        // 密碼重設相關
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetExpires { get; set; }

        // 導航屬性
        public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    }
}
