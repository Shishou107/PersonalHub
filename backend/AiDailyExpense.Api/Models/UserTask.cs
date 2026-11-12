using System;
using System.Collections.Generic;

namespace AiDailyExpense.Api.Models
{
    public class UserTask
    {
        public long TaskId { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime? DueAt { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Done, Cancelled
        public string Priority { get; set; } = "Normal"; // Low, Normal, High
        public string RepeatType { get; set; } = "None"; // None, Daily, Weekly, Monthly, Yearly
        public string TaskType { get; set; } = "Task"; // Task, Schedule
        public string? Location { get; set; }
        public string? RawText { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
        public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
    }
}
