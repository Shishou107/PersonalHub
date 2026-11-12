using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Reports;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportsController : ControllerBase
    {
        private readonly IReportService _reportService;
        private readonly CurrentUserHelper _currentUserHelper;

        public ReportsController(IReportService reportService, CurrentUserHelper currentUserHelper)
        {
            _reportService = reportService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var result = await _reportService.GetDashboardAsync(userId);
                return Ok(ApiResponse<DashboardDto>.Ok(result, "Lấy thông tin tổng hợp thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<DashboardDto>.Fail(ex.Message));
            }
        }

        [HttpGet("monthly-expense")]
        public async Task<IActionResult> GetMonthlyExpense([FromQuery] int? month, [FromQuery] int? year)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var targetMonth = month ?? DateTime.UtcNow.Month;
                var targetYear = year ?? DateTime.UtcNow.Year;

                var result = await _reportService.GetMonthlyExpenseReportAsync(userId, targetMonth, targetYear);
                return Ok(ApiResponse<MonthlyExpenseReportDto>.Ok(result, "Lấy báo cáo chi tiêu thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<MonthlyExpenseReportDto>.Fail(ex.Message));
            }
        }
    }
}
