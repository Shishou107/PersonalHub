using System;
using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.Expenses
{
    public class UpdateExpenseRequest
    {
        [Required(ErrorMessage = "Tiêu đề là bắt buộc.")]
        [StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Số tiền là bắt buộc.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0.")]
        public decimal Amount { get; set; }

        public int? CategoryId { get; set; }

        [RegularExpression("^(Cash|Transfer)$")]
        public string PaymentMethod { get; set; } = "Cash";

        [Required(ErrorMessage = "Thời gian chi tiêu là bắt buộc.")]
        public DateTime SpentAt { get; set; }

        [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
        public string? Note { get; set; }
    }
}
