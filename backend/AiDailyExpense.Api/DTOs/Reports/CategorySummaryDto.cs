namespace AiDailyExpense.Api.DTOs.Reports
{
    public class CategorySummaryDto
    {
        public string CategoryName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public double Percent { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
    }
}
