using System;

namespace AiDailyExpense.Api.DTOs.Auth
{
    public class UserDto
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public decimal CashBalance { get; set; }
        public decimal BankBalance { get; set; }
        public decimal TotalBalance { get; set; }
        public bool HasInitializedBalance { get; set; }
        public bool HasVaultPin { get; set; }
    }
}
