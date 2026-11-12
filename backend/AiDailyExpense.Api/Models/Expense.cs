using System;

namespace AiDailyExpense.Api.Models
{
    public class Expense
    {
        public long ExpenseId { get; set; }
        public Guid UserId { get; set; }
        public int? CategoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime SpentAt { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string? Note { get; set; }
        public string? RawText { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public User User { get; set; } = null!;
        public ExpenseCategory? Category { get; set; }
    }
}
