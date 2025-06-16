using System;
using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models
{
    public class OperationLog
    {
        [Key]
        public int Id { get; set; }
        public int? EngineerId { get; set; }
        public Engineer? Engineer { get; set; }

        public DateTime ActionTime { get; set; } = DateTime.UtcNow;
        [Required]
        public string Controller { get; set; } = string.Empty;
        [Required]
        public string Action { get; set; } = string.Empty;

        public string? TargetId { get; set; }

        public string? Description { get; set; }
        public DateTime Timestamp { get; internal set; } = DateTime.UtcNow;
    }
}
