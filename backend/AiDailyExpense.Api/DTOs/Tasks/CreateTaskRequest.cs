using System;
using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.Tasks
{
    public class CreateTaskRequest
    {
        [Required(ErrorMessage = "Tiêu đề công việc là bắt buộc.")]
        [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000, ErrorMessage = "Mô tả tối đa 1000 ký tự.")]
        public string? Description { get; set; }

        public DateTime? StartAt { get; set; }

        public DateTime? EndAt { get; set; }

        public DateTime? DueAt { get; set; }

        [RegularExpression("^(Low|Normal|High)$", ErrorMessage = "Độ ưu tiên không hợp lệ.")]
        public string Priority { get; set; } = "Normal";

        [RegularExpression("^(None|Daily|Weekly|Monthly|Yearly)$", ErrorMessage = "Loại lặp lại không hợp lệ.")]
        public string RepeatType { get; set; } = "None";

        public int? ReminderBeforeMinutes { get; set; }
        
        [RegularExpression("^(Task|Schedule)$", ErrorMessage = "Loại công việc/lịch trình không hợp lệ.")]
        public string TaskType { get; set; } = "Task";

        [StringLength(200, ErrorMessage = "Địa điểm tối đa 200 ký tự.")]
        public string? Location { get; set; }
        
        public string? RawText { get; set; }
    }
}
