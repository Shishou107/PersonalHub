using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.BankTransactions;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/bank-transactions")]
    [Authorize]
    public class BankTransactionsController : ControllerBase
    {
        private readonly IBankTransactionService _bankTransactionService;
        private readonly CurrentUserHelper _currentUserHelper;

        public BankTransactionsController(IBankTransactionService bankTransactionService, CurrentUserHelper currentUserHelper)
        {
            _bankTransactionService = bankTransactionService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpGet]
        public async Task<IActionResult> GetBankTransactions([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var txs = await _bankTransactionService.GetBankTransactionsAsync(userId, fromDate, toDate);
                return Ok(ApiResponse<List<BankTransactionDto>>.Ok(txs, "Lấy danh sách giao dịch ngân hàng thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<BankTransactionDto>>.Fail(ex.Message));
            }
        }

        [HttpPost("from-notification")]
        public async Task<IActionResult> ProcessNotification([FromBody] BankNotificationRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var tx = await _bankTransactionService.ProcessNotificationAsync(userId, request);
                return Ok(ApiResponse<BankTransactionDto>.Ok(tx, "Phân tích và lưu thông báo giao dịch thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<BankTransactionDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<BankTransactionDto>.Fail("Có lỗi xảy ra khi xử lý thông báo.", new List<string> { ex.Message }));
            }
        }

        [HttpPost("{id}/confirm")]
        public async Task<IActionResult> ConfirmTransaction(long id, [FromBody] ConfirmBankTransactionRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var expense = await _bankTransactionService.ConfirmTransactionAsync(userId, id, request);
                return Ok(ApiResponse<ExpenseDto>.Ok(expense, "Xác nhận giao dịch và tạo khoản chi tiêu thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<ExpenseDto>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<ExpenseDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ExpenseDto>.Fail("Có lỗi xảy ra khi xác nhận giao dịch.", new List<string> { ex.Message }));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTransaction(long id)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var success = await _bankTransactionService.DeleteTransactionAsync(userId, id);
                if (!success)
                {
                    return NotFound(ApiResponse<bool>.Fail("Không tìm thấy giao dịch ngân hàng hoặc xóa không thành công."));
                }
                return Ok(ApiResponse<bool>.Ok(true, "Xóa giao dịch thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }
    }
}
