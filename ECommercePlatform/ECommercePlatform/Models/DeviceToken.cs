using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models
{
    public class DeviceToken
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Token { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int? UserId { get; set; }
    }
}
