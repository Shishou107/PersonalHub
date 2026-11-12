using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.Auth
{
    public class InitializeBalanceRequest
    {
        [Range(0, double.MaxValue)]
        public decimal CashBalance { get; set; }

        [Range(0, double.MaxValue)]
        public decimal BankBalance { get; set; }
    }
}
