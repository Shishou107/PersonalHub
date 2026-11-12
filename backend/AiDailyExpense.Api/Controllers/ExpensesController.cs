using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExpensesController : ControllerBase
    {
        private readonly IExpenseService _expenseService;
        private readonly CurrentUserHelper _currentUserHelper;

        public ExpensesController(IExpenseService expenseService, CurrentUserHelper currentUserHelper)
        {
            _expenseService = expenseService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpGet]
        public async Task<IActionResult> GetExpenses([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, [FromQuery] int? page = null, [FromQuery] int? pageSize = null)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var expenses = await _expenseService.GetExpensesAsync(userId, fromDate, toDate, page, pageSize);
                return Ok(ApiResponse<List<ExpenseDto>>.Ok(expenses, "Lấy danh sách chi tiêu thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<ExpenseDto>>.Fail(ex.Message));
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetExpenseById(long id)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var expense = await _expenseService.GetExpenseByIdAsync(userId, id);
                if (expense == null)
                {
                    return NotFound(ApiResponse<ExpenseDto>.Fail("Không tìm thấy khoản chi tiêu."));
                }
                return Ok(ApiResponse<ExpenseDto>.Ok(expense, "Lấy chi tiết chi tiêu thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<ExpenseDto>.Fail(ex.Message));
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var expense = await _expenseService.CreateExpenseAsync(userId, request);
                return Ok(ApiResponse<ExpenseDto>.Ok(expense, "Ghi nhận khoản chi tiêu thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<ExpenseDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ExpenseDto>.Fail("Có lỗi xảy ra khi tạo khoản chi tiêu.", new List<string> { ex.Message }));
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateExpense(long id, [FromBody] UpdateExpenseRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var expense = await _expenseService.UpdateExpenseAsync(userId, id, request);
                return Ok(ApiResponse<ExpenseDto>.Ok(expense, "Cập nhật khoản chi tiêu thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<ExpenseDto>.Fail(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<ExpenseDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ExpenseDto>.Fail("Có lỗi xảy ra khi cập nhật khoản chi tiêu.", new List<string> { ex.Message }));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteExpense(long id)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var success = await _expenseService.DeleteExpenseAsync(userId, id);
                if (!success)
                {
                    return NotFound(ApiResponse<bool>.Fail("Không tìm thấy khoản chi tiêu hoặc xóa không thành công."));
                }
                return Ok(ApiResponse<bool>.Ok(true, "Xóa khoản chi tiêu thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }
    }
}
