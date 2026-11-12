using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.Tasks
{
    public class CreateTaskBatchRequest
    {
        [Required(ErrorMessage = "Danh sách công việc không được để trống.")]
        [MinLength(1, ErrorMessage = "Cần ít nhất một công việc để tạo.")]
        public List<CreateTaskRequest> Tasks { get; set; } = new();
    }
}
