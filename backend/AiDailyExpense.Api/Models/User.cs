using System;
using System.Collections.Generic;

namespace AiDailyExpense.Api.Models
{
    public class User
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsEmailVerified { get; set; }
        public DateTime? EmailVerifiedAt { get; set; }
        public string? VaultPinHash { get; set; }
        public decimal CashBalance { get; set; } = 0;
        public decimal BankBalance { get; set; } = 0;
        public bool HasInitializedBalance { get; set; } = false;
        public string TimeZone { get; set; } = "Asia/Ho_Chi_Minh";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<ExpenseCategory> ExpenseCategories { get; set; } = new List<ExpenseCategory>();
        public ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public ICollection<UserTask> UserTasks { get; set; } = new List<UserTask>();
        public ICollection<Reminder> Reminders { get; set; } = new List<Reminder>();
        public ICollection<AiCommandLog> AiCommandLogs { get; set; } = new List<AiCommandLog>();
        public ICollection<BankTransaction> BankTransactions { get; set; } = new List<BankTransaction>();
        public ICollection<DeviceToken> DeviceTokens { get; set; } = new List<DeviceToken>();
        public ICollection<EmailVerificationCode> EmailVerificationCodes { get; set; } = new List<EmailVerificationCode>();
        public ICollection<VaultItem> VaultItems { get; set; } = new List<VaultItem>();
        public ICollection<VaultCategory> VaultCategories { get; set; } = new List<VaultCategory>();
        public ICollection<StudyDeck> StudyDecks { get; set; } = new List<StudyDeck>();
    }
}
