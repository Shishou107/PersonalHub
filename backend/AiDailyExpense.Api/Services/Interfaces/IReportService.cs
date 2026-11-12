using System;
using System.Threading.Tasks;
using AiDailyExpense.Api.DTOs.Reports;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface IReportService
    {
        Task<DashboardDto> GetDashboardAsync(Guid userId);
        Task<MonthlyExpenseReportDto> GetMonthlyExpenseReportAsync(Guid userId, int month, int year);
    }
}
