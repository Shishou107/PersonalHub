using System;

namespace AiDailyExpense.Api.Models
{
    public class Reminder
    {
        public long ReminderId { get; set; }
        public Guid UserId { get; set; }
        public long? TaskId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime RemindAt { get; set; }
        public bool IsSent { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User User { get; set; } = null!;
        public UserTask? Task { get; set; }
    }
}
