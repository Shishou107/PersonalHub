using System;
using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.BankTransactions
{
    public class BankNotificationRequest
    {
        [StringLength(100, ErrorMessage = "Tên ngân hàng tối đa 100 ký tự.")]
        public string? BankName { get; set; }

        [Required(ErrorMessage = "Nội dung tin nhắn SMS/Notification là bắt buộc.")]
        [StringLength(1000, ErrorMessage = "Nội dung tối đa 1000 ký tự.")]
        public string RawMessage { get; set; } = string.Empty;

        public DateTime? ReceivedAt { get; set; }
    }
}
