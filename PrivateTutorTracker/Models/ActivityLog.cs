using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PrivateTutorTracker.Models
{
    public class ActivityLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty; // Create, Update, Delete, View, Export, Login, Logout, etc.

        [Required]
        [MaxLength(100)]
        public string EntityType { get; set; } = string.Empty; // Student, Lesson, Payment, Report, etc.

        public int? EntityId { get; set; } // ID of the affected entity (nullable for actions like Login)

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty; // Human-readable description

        public string? Details { get; set; } // JSON for additional data

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }
    }
}
