using System;

namespace AiDailyExpense.Api.Models
{
    public class BankTransaction
    {
        public long BankTransactionId { get; set; }
        public Guid UserId { get; set; }
        public string? BankName { get; set; }
        public string? AccountLabel { get; set; }
        public string TransactionType { get; set; } = "Unknown"; // Income, Expense, Transfer, Unknown
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public DateTime? TransactionTime { get; set; }
        public string Source { get; set; } = "Notification"; // Notification, Manual, Import, BankApi
        public string? RawMessage { get; set; }
        public string? RawMessageHash { get; set; }
        public bool IsConfirmed { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User User { get; set; } = null!;
        public ExpenseCategory? Category { get; set; }
    }
}
