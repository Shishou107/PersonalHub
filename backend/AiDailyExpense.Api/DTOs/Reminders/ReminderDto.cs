using System;

namespace AiDailyExpense.Api.DTOs.Reminders
{
    public class ReminderDto
    {
        public long ReminderId { get; set; }
        public Guid UserId { get; set; }
        public long? TaskId { get; set; }
        public string? TaskTitle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Message { get; set; }
        public DateTime RemindAt { get; set; }
        public bool IsSent { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
