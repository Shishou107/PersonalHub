using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Tasks;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TasksController : ControllerBase
    {
        private readonly ITaskService _taskService;
        private readonly CurrentUserHelper _currentUserHelper;

        public TasksController(ITaskService taskService, CurrentUserHelper currentUserHelper)
        {
            _taskService = taskService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpGet]
        public async Task<IActionResult> GetTasks([FromQuery] DateTime? date, [FromQuery] string? status)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var tasks = await _taskService.GetTasksAsync(userId, date, status);
                return Ok(ApiResponse<List<TaskDto>>.Ok(tasks, "Lấy danh sách công việc thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<TaskDto>>.Fail(ex.Message));
            }
        }

        [HttpGet("today")]
        public async Task<IActionResult> GetTodayTasks()
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var tasks = await _taskService.GetTodayTasksAsync(userId);
                return Ok(ApiResponse<List<TaskDto>>.Ok(tasks, "Lấy danh sách công việc hôm nay thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<TaskDto>>.Fail(ex.Message));
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTaskById(long id)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var task = await _taskService.GetTaskByIdAsync(userId, id);
                if (task == null)
                {
                    return NotFound(ApiResponse<TaskDto>.Fail("Không tìm thấy công việc."));
                }
                return Ok(ApiResponse<TaskDto>.Ok(task, "Lấy chi tiết công việc thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<TaskDto>.Fail(ex.Message));
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var task = await _taskService.CreateTaskAsync(userId, request);
                return Ok(ApiResponse<TaskDto>.Ok(task, "Tạo công việc thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<TaskDto>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<TaskDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<TaskDto>.Fail("Có lỗi xảy ra khi tạo công việc.", new List<string> { ex.Message }));
            }
        }

        [HttpPost("batch")]
        public async Task<IActionResult> CreateBatchTasks([FromBody] CreateTaskBatchRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                if (request == null || request.Tasks == null || request.Tasks.Count == 0)
                {
                    return BadRequest(ApiResponse<List<TaskDto>>.Fail("Danh sách công việc không được để trống."));
                }

                var tasks = await _taskService.CreateTasksBatchAsync(userId, request.Tasks);
                return Ok(ApiResponse<List<TaskDto>>.Ok(tasks, $"Đã tạo thành công {tasks.Count} công việc/lịch trình."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<List<TaskDto>>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<List<TaskDto>>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<List<TaskDto>>.Fail("Có lỗi xảy ra khi tạo danh sách công việc.", new List<string> { ex.Message }));
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTask(long id, [FromBody] UpdateTaskRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var task = await _taskService.UpdateTaskAsync(userId, id, request);
                return Ok(ApiResponse<TaskDto>.Ok(task, "Cập nhật công việc thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<TaskDto>.Fail(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<TaskDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<TaskDto>.Fail("Có lỗi xảy ra khi cập nhật công việc.", new List<string> { ex.Message }));
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask(long id)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var success = await _taskService.DeleteTaskAsync(userId, id);
                if (!success)
                {
                    return NotFound(ApiResponse<bool>.Fail("Không tìm thấy công việc hoặc xóa không thành công."));
                }
                return Ok(ApiResponse<bool>.Ok(true, "Xóa công việc thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }

        [HttpPost("{id}/complete")]
        public async Task<IActionResult> CompleteTask(long id)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var task = await _taskService.CompleteTaskAsync(userId, id);
                return Ok(ApiResponse<TaskDto>.Ok(task, "Hoàn thành công việc thành công"));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<TaskDto>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<TaskDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<TaskDto>.Fail("Có lỗi xảy ra khi hoàn thành công việc.", new List<string> { ex.Message }));
            }
        }
    }
}
