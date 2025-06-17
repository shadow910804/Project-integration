using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models
{
    public class FixPassword
    {
        [Required(ErrorMessage = "舊密碼為必填")]
        public string OldPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "新密碼為必填")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "密碼長度必須在6-100字元之間")]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "確認密碼為必填")]
        [Compare("NewPassword", ErrorMessage = "確認密碼與新密碼不一致")]
        public string CheckPassword { get; set; } = string.Empty;
    }
}
