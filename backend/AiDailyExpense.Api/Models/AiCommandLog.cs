using System;

namespace AiDailyExpense.Api.Models
{
    public class AiCommandLog
    {
        public long LogId { get; set; }
        public Guid UserId { get; set; }
        public string RawInput { get; set; } = string.Empty;
        public string? Intent { get; set; }
        public string? ParsedJson { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User User { get; set; } = null!;
    }
}
