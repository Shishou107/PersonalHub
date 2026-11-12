namespace AiDailyExpense.Api.DTOs.Ai
{
    public class AiParsedFinancialNotificationDto
    {
        public bool IsTransaction { get; set; }
        public string IgnoreReason { get; set; } = string.Empty;
        public string TransactionType { get; set; } = "Unknown";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "VND";
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string CategoryName { get; set; } = "Khác";
        public string Provider { get; set; } = string.Empty;
        public string AccountLabel { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
    }
}
