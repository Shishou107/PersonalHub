using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Ai;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AiController : ControllerBase
    {
        private readonly IAiService _aiService;
        private readonly CurrentUserHelper _currentUserHelper;

        public AiController(IAiService aiService, CurrentUserHelper currentUserHelper)
        {
            _aiService = aiService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpPost("parse")]
        public async Task<IActionResult> ParseText([FromBody] AiParseRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var result = await _aiService.ParseAsync(userId, request);

                string message = "Xử lý thành công câu lệnh.";
                if (result.Intent == "add_expense" && result.SavedItems != null)
                {
                    var hasIncome = result.SavedItems.Any(i => string.Equals(i.CategoryType, "Income", StringComparison.OrdinalIgnoreCase));
                    var hasExpense = result.SavedItems.Any(i => string.Equals(i.CategoryType, "Expense", StringComparison.OrdinalIgnoreCase));
                    if (hasIncome && hasExpense)
                    {
                        message = $"Đã lưu {result.SavedItems.Count} giao dịch tài chính.";
                    }
                    else if (hasIncome)
                    {
                        message = $"Đã lưu {result.SavedItems.Count} khoản thu nhập.";
                    }
                    else
                    {
                        message = $"Đã lưu {result.SavedItems.Count} khoản chi tiêu.";
                    }
                }
                else if (result.Intent == "add_task" && result.SavedTasks != null && result.SavedTasks.Count > 0)
                {
                    var hasSchedule = result.SavedTasks.Any(t => string.Equals(t.TaskType, "Schedule", StringComparison.OrdinalIgnoreCase));
                    message = hasSchedule
                        ? $"Da tao {result.SavedTasks.Count} muc lich trinh thanh cong."
                        : $"Da tao {result.SavedTasks.Count} cong viec thanh cong.";
                }
                else if (result.Intent == "add_task" && result.Task != null)
                {
                    message = "Đã tạo công việc thành công.";
                }
                else if (result.Intent == "unknown")
                {
                    return BadRequest(ApiResponse<AiParseResponse>.Fail("Đã hết token, vui lòng liên hệ admin!"));
                }

                return Ok(ApiResponse<AiParseResponse>.Ok(result, message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<AiParseResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Contains("admin") ? ex.Message : "Đã hết token, vui lòng liên hệ admin!";
                return BadRequest(ApiResponse<AiParseResponse>.Fail(msg));
            }
        }

        [HttpPost("parse-timetable-image")]
        [RequestSizeLimit(10_000_000)]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ParseTimetableImage([FromForm] TimetableImageUploadRequest request)
        {
            try
            {
                var image = request?.Image;
                if (image == null || image.Length == 0)
                {
                    return BadRequest(ApiResponse<AiTimetableParseResponse>.Fail("Vui lòng chọn ảnh thời khóa biểu."));
                }

                if (!image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse<AiTimetableParseResponse>.Fail("File gửi lên phải là ảnh."));
                }

                if (image.Length > 10_000_000)
                {
                    return BadRequest(ApiResponse<AiTimetableParseResponse>.Fail("Ảnh tối đa 10MB."));
                }

                await using var stream = new MemoryStream();
                await image.CopyToAsync(stream);

                var userId = _currentUserHelper.GetUserId();
                var result = await _aiService.ParseTimetableImageAsync(userId, stream.ToArray(), image.ContentType);

                if (result.SavedTasks.Count == 0)
                {
                    return BadRequest(ApiResponse<AiTimetableParseResponse>.Fail("Đã hết token, vui lòng liên hệ admin!"));
                }

                return Ok(ApiResponse<AiTimetableParseResponse>.Ok(
                    result,
                    $"Đã tạo {result.SavedTasks.Count} công việc từ thời khóa biểu."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<AiTimetableParseResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Contains("admin") ? ex.Message : "Đã hết token, vui lòng liên hệ admin!";
                return BadRequest(ApiResponse<AiTimetableParseResponse>.Fail(msg));
            }
        }

        [HttpPost("vocabulary")]
        public async Task<IActionResult> AnalyzeVocabulary([FromBody] AiVocabularyRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var result = await _aiService.AnalyzeVocabularyAsync(userId, request);
                return Ok(ApiResponse<AiVocabularyResponse>.Ok(result, "Tao thong tin tu vung thanh cong"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<AiVocabularyResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Contains("admin") ? ex.Message : "Da het token, vui long lien he admin!";
                return BadRequest(ApiResponse<AiVocabularyResponse>.Fail(msg));
            }
        }

        [HttpPost("vocabulary/analyze")]
        public async Task<IActionResult> AnalyzeVocabularyExtended([FromBody] AiVocabularyAnalyzeRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var result = await _aiService.AnalyzeVocabularyExtendedAsync(userId, request);
                return Ok(ApiResponse<AiVocabularyAnalyzeResponse>.Ok(result, "Phan tich tu vung thanh cong"));
            }
            catch (NotSupportedException ex)
            {
                return BadRequest(ApiResponse<AiVocabularyAnalyzeResponse>.Fail(ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<AiVocabularyAnalyzeResponse>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<AiVocabularyAnalyzeResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Contains("admin") ? ex.Message : "Da het token, vui long lien he admin!";
                return BadRequest(ApiResponse<AiVocabularyAnalyzeResponse>.Fail(msg));
            }
        }
    }

    public class TimetableImageUploadRequest
    {
        public Microsoft.AspNetCore.Http.IFormFile? Image { get; set; }
    }
}
