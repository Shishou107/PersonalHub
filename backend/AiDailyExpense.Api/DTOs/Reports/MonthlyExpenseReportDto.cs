using System.Collections.Generic;

namespace AiDailyExpense.Api.DTOs.Reports
{
    public class MonthlyExpenseReportDto
    {
        public int Month { get; set; }
        public int Year { get; set; }
        public decimal TotalExpense { get; set; }
        public List<CategorySummaryDto> ByCategory { get; set; } = new List<CategorySummaryDto>();
        public string AiSummary { get; set; } = string.Empty;
    }
}
