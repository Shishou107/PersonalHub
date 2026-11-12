using System;

namespace AiDailyExpense.Api.DTOs.Expenses
{
    public class ExpenseDto
    {
        public long ExpenseId { get; set; }
        public Guid UserId { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public string? CategoryIcon { get; set; }
        public string? CategoryColor { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime SpentAt { get; set; }
        public string PaymentMethod { get; set; } = "Cash";
        public string CategoryType { get; set; } = "Expense";
        public string? Note { get; set; }
        public string? RawText { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
