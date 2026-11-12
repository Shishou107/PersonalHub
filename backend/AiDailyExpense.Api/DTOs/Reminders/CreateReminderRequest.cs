using System;
using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.Reminders
{
    public class CreateReminderRequest
    {
        public long? TaskId { get; set; }

        [Required(ErrorMessage = "Tiêu đề nhắc nhở là bắt buộc.")]
        [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Nội dung tối đa 500 ký tự.")]
        public string? Message { get; set; }

        [Required(ErrorMessage = "Thời gian nhắc nhở là bắt buộc.")]
        public DateTime RemindAt { get; set; }
    }
}
