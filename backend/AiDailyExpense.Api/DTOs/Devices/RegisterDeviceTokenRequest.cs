using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.Devices
{
    public class RegisterDeviceTokenRequest
    {
        [Required(ErrorMessage = "Token thiết bị là bắt buộc.")]
        [StringLength(500, ErrorMessage = "Token tối đa 500 ký tự.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "Platform thiết bị là bắt buộc.")]
        [RegularExpression("^(Android|iOS|Web)$", ErrorMessage = "Platform phải là Android, iOS hoặc Web.")]
        public string Platform { get; set; } = string.Empty;
    }
}
