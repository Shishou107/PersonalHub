using System.Collections.Generic;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.DTOs.Tasks;

namespace AiDailyExpense.Api.DTOs.Reports
{
    public class DashboardDto
    {
        public decimal TodayExpense { get; set; }
        public decimal MonthExpense { get; set; }
        public decimal CashBalance { get; set; }
        public decimal BankBalance { get; set; }
        public decimal TotalBalance { get; set; }
        public int PendingTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public List<ExpenseDto> RecentExpenses { get; set; } = new List<ExpenseDto>();
        public List<TaskDto> TodayTasks { get; set; } = new List<TaskDto>();
    }
}
