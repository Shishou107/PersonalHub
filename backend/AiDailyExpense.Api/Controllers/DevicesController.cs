using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Devices;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DevicesController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly CurrentUserHelper _currentUserHelper;

        public DevicesController(INotificationService notificationService, CurrentUserHelper currentUserHelper)
        {
            _notificationService = notificationService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpPost("register-token")]
        public async Task<IActionResult> RegisterToken([FromBody] RegisterDeviceTokenRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var result = await _notificationService.RegisterDeviceTokenAsync(userId, request);
                return Ok(ApiResponse<bool>.Ok(result, "Đăng ký token thiết bị thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }
    }
}
