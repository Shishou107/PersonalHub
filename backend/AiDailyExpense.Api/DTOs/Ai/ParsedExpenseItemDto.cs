namespace AiDailyExpense.Api.DTOs.Ai
{
    public class ParsedExpenseItemDto
    {
        public long ExpenseId { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public string CategoryType { get; set; } = "Expense";
        public string PaymentMethod { get; set; } = "Cash";
    }
}
