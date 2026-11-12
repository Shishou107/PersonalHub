using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.Ai
{
    public class AiParseRequest
    {
        [Required(ErrorMessage = "Nội dung câu lệnh là bắt buộc.")]
        [StringLength(2000, ErrorMessage = "Câu lệnh tối đa 2000 ký tự.")]
        public string Text { get; set; } = string.Empty;
    }
}
