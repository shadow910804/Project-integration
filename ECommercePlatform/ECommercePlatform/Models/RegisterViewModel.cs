using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models
{
    public class RegisterViewModel
    {
        [Required(ErrorMessage = "用戶名為必填")]
        [StringLength(50, ErrorMessage = "用戶名長度不能超過50字元")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email 為必填")]
        [EmailAddress(ErrorMessage = "請輸入有效的 Email 格式")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "密碼為必填")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密碼長度必須在6-100字元之間")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "確認密碼為必填")]
        [Compare("Password", ErrorMessage = "確認密碼與密碼不符")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "名字為必填")]
        [StringLength(50, ErrorMessage = "名字長度不能超過50字元")]
        public string? FirstName { get; set; }

        [Required(ErrorMessage = "姓氏為必填")]
        [StringLength(50, ErrorMessage = "姓氏長度不能超過50字元")]
        public string? LastName { get; set; }
    }
}
