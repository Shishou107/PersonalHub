using System;
using System.Collections.Generic;

namespace AiDailyExpense.Api.Models
{
    public class ExpenseCategory
    {
        public int CategoryId { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Expense"; // Expense or Income
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool IsDefault { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public User? User { get; set; }
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public ICollection<BankTransaction> BankTransactions { get; set; } = new List<BankTransaction>();
    }
}
