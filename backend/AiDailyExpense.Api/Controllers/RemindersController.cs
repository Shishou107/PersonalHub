using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Reminders;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RemindersController : ControllerBase
    {
        private readonly IReminderService _reminderService;
        private readonly CurrentUserHelper _currentUserHelper;

        public RemindersController(IReminderService reminderService, CurrentUserHelper currentUserHelper)
        {
            _reminderService = reminderService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpGet]
        public async Task<IActionResult> GetReminders([FromQuery] bool upcoming = true)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var reminders = await _reminderService.GetRemindersAsync(userId, upcoming);
                return Ok(ApiResponse<List<ReminderDto>>.Ok(reminders, "Lấy danh sách nhắc nhở thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<ReminderDto>>.Fail(ex.Message));
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateReminder([FromBody] CreateReminderRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var reminder = await _reminderService.CreateReminderAsync(userId, request);
                return Ok(ApiResponse<ReminderDto>.Ok(reminder, "Tạo nhắc nhở thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<ReminderDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<ReminderDto>.Fail("Có lỗi xảy ra khi tạo nhắc nhở.", new List<string> { ex.Message }));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReminder(long id)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var success = await _reminderService.DeleteReminderAsync(userId, id);
                if (!success)
                {
                    return NotFound(ApiResponse<bool>.Fail("Không tìm thấy nhắc nhở hoặc xóa không thành công."));
                }
                return Ok(ApiResponse<bool>.Ok(true, "Xóa nhắc nhở thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }
    }
}
