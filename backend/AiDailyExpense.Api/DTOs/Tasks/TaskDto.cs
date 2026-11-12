using System;

namespace AiDailyExpense.Api.DTOs.Tasks
{
    public class TaskDto
    {
        public long TaskId { get; set; }
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime? StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public DateTime? DueAt { get; set; }
        public string Status { get; set; } = "Pending";
        public string Priority { get; set; } = "Normal";
        public string RepeatType { get; set; } = "None";
        public string? RawText { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public bool IsOverdue { get; set; }
        public int? ReminderBeforeMinutes { get; set; }
        public string TaskType { get; set; } = "Task";
        public string? Location { get; set; }
    }
}
