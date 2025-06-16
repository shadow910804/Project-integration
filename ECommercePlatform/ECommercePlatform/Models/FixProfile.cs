using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models
{
    public class FixProfile
    {
        [Required(ErrorMessage = "名字是必填的")]
        public string? FirstName { get; set; }
        [Required(ErrorMessage = "姓氏是必填的")]
        public string? LastName { get; set; }
        [Required(ErrorMessage = "電話是必填的")]
        [RegularExpression(@"^09\d{8}", ErrorMessage ="電話格式錯誤")]
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
    }
}
