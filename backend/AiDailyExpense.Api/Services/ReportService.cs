using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.DTOs.Reports;
using AiDailyExpense.Api.DTOs.Tasks;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class ReportService : IReportService
    {
        private readonly AppDbContext _context;
        private readonly IExpenseService _expenseService;
        private readonly ITaskService _taskService;

        public ReportService(AppDbContext context, IExpenseService expenseService, ITaskService taskService)
        {
            _context = context;
            _expenseService = expenseService;
            _taskService = taskService;
        }

        public async Task<DashboardDto> GetDashboardAsync(Guid userId)
        {
            var user = await _context.Users.FirstAsync(u => u.UserId == userId);
            var timeZoneId = user.TimeZone ?? "Asia/Ho_Chi_Minh";
            var tz = Helpers.DateTimeHelper.GetUserTimeZone(timeZoneId);
            
            var userLocalTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var todayLocal = userLocalTime.Date;
            var startOfMonthLocal = new DateTime(todayLocal.Year, todayLocal.Month, 1);
            var nextMonthLocal = startOfMonthLocal.AddMonths(1);

            var (todayUtc, tomorrowUtc) = Helpers.DateTimeHelper.GetUtcRangeForLocalDate(todayLocal, timeZoneId);
            var (startOfMonthUtc, nextMonthUtc) = Helpers.DateTimeHelper.GetUtcRangeForLocalDate(startOfMonthLocal, timeZoneId);
            var nextMonthUtcStart = Helpers.DateTimeHelper.GetUtcRangeForLocalDate(nextMonthLocal, timeZoneId).StartUtc;

            // Today expense sum
            var todayExpense = await _context.Expenses
                .Include(e => e.Category)
                .Where(e => e.UserId == userId && e.SpentAt >= todayUtc && e.SpentAt < tomorrowUtc && (e.Category == null || e.Category.Type == "Expense"))
                .SumAsync(e => e.Amount);

            // Month expense sum
            var monthExpense = await _context.Expenses
                .Include(e => e.Category)
                .Where(e => e.UserId == userId && e.SpentAt >= startOfMonthUtc && e.SpentAt < nextMonthUtcStart && (e.Category == null || e.Category.Type == "Expense"))
                .SumAsync(e => e.Amount);

            // Tasks counts
            var pendingTasks = await _context.UserTasks.CountAsync(t => t.UserId == userId && t.Status == "Pending" && t.TaskType == "Task");
            var completedTasks = await _context.UserTasks.CountAsync(t => t.UserId == userId && t.Status == "Done" && t.TaskType == "Task");
            var overdueTasks = await _context.UserTasks.CountAsync(t => t.UserId == userId && t.Status == "Pending" && t.TaskType == "Task" && t.DueAt.HasValue && t.DueAt.Value < DateTime.UtcNow);

            // Recent expenses (top 5)
            var recentExpenses = await _expenseService.GetExpensesAsync(userId, null, null);
            recentExpenses = recentExpenses.Take(5).ToList();

            // Today tasks
            var todayTasks = await _taskService.GetTodayTasksAsync(userId);

            return new DashboardDto
            {
                TodayExpense = todayExpense,
                MonthExpense = monthExpense,
                CashBalance = user.CashBalance,
                BankBalance = user.BankBalance,
                TotalBalance = user.CashBalance + user.BankBalance,
                PendingTasks = pendingTasks,
                CompletedTasks = completedTasks,
                OverdueTasks = overdueTasks,
                RecentExpenses = recentExpenses,
                TodayTasks = todayTasks
            };
        }

        public async Task<MonthlyExpenseReportDto> GetMonthlyExpenseReportAsync(Guid userId, int month, int year)
        {
            var user = await _context.Users.FirstAsync(u => u.UserId == userId);
            var timeZoneId = user.TimeZone ?? "Asia/Ho_Chi_Minh";
            var tz = Helpers.DateTimeHelper.GetUserTimeZone(timeZoneId);

            var startDateLocal = new DateTime(year, month, 1);
            var endDateLocal = startDateLocal.AddMonths(1);

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startDateLocal, DateTimeKind.Unspecified), tz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endDateLocal, DateTimeKind.Unspecified), tz);

            var expenses = await _context.Expenses
                .Include(e => e.Category)
                .Where(e => e.UserId == userId && e.SpentAt >= startUtc && e.SpentAt < endUtc && (e.Category == null || e.Category.Type == "Expense"))
                .ToListAsync();

            var totalExpense = expenses.Sum(e => e.Amount);

            var grouped = expenses
                .GroupBy(e => new
                {
                    CategoryName = e.Category != null ? e.Category.Name : "Khác",
                    Icon = e.Category != null ? e.Category.Icon : "other",
                    Color = e.Category != null ? e.Category.Color : "#6B7280"
                })
                .Select(g => new CategorySummaryDto
                {
                    CategoryName = g.Key.CategoryName,
                    TotalAmount = g.Sum(e => e.Amount),
                    Percent = totalExpense > 0 ? Math.Round((double)(g.Sum(e => e.Amount) / totalExpense) * 100, 2) : 0,
                    Icon = g.Key.Icon,
                    Color = g.Key.Color
                })
                .OrderByDescending(c => c.TotalAmount)
                .ToList();

            // Generate AI Summary string
            string aiSummary = "Tháng này bạn chưa ghi chép chi tiêu nào.";
            if (totalExpense > 0 && grouped.Count > 0)
            {
                var highestSpent = grouped.First();
                aiSummary = $"Tháng này bạn tiêu nhiều nhất vào mục '{highestSpent.CategoryName}' với {highestSpent.TotalAmount:N0}đ (chiếm {highestSpent.Percent}%).";
                
                if (highestSpent.CategoryName == "Ăn uống" && highestSpent.Percent > 40)
                {
                    aiSummary += " Hãy cân nhắc tự nấu ăn ở nhà để tiết kiệm chi phí ăn ngoài nhé!";
                }
                else if (highestSpent.CategoryName == "Giải trí" && highestSpent.Percent > 30)
                {
                    aiSummary += " Hạn chế các buổi vui chơi để ổn định ngân sách.";
                }
            }

            return new MonthlyExpenseReportDto
            {
                Month = month,
                Year = year,
                TotalExpense = totalExpense,
                ByCategory = grouped,
                AiSummary = aiSummary
            };
        }
    }
}
