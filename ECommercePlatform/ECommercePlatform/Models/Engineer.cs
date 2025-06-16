using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ECommercePlatform.Models
{
    public class Engineer
    {
        public int Id { get; set; }
        public string Username { get; set; } = null!;
        public string PasswordHash { get; set; } = null!;
        public String Email { get; set; } = null!;
        public ICollection<OperationLog> OperationLogs { get; set; } = new List<OperationLog>();
    }
}
  
