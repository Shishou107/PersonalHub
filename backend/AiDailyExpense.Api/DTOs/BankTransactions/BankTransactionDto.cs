using System;

namespace AiDailyExpense.Api.DTOs.BankTransactions
{
    public class BankTransactionDto
    {
        public long BankTransactionId { get; set; }
        public Guid UserId { get; set; }
        public string? BankName { get; set; }
        public string? AccountLabel { get; set; }
        public string TransactionType { get; set; } = "Unknown";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string? Description { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public DateTime? TransactionTime { get; set; }
        public string Source { get; set; } = "Notification";
        public string? RawMessage { get; set; }
        public bool IsConfirmed { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
