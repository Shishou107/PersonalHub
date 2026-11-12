using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Auth;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly CurrentUserHelper _currentUserHelper;

        public AuthController(IAuthService authService, CurrentUserHelper currentUserHelper)
        {
            _authService = authService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var result = await _authService.RegisterAsync(request);
                return Ok(ApiResponse<AuthResponse>.Ok(result, "Đăng ký thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<AuthResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<AuthResponse>.Fail("Có lỗi xảy ra trong quá trình đăng ký.", new List<string> { ex.Message }));
            }
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                return Ok(ApiResponse<AuthResponse>.Ok(result, "Đăng nhập thành công"));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<AuthResponse>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<AuthResponse>.Fail("Có lỗi xảy ra trong quá trình đăng nhập.", new List<string> { ex.Message }));
            }
        }

        [HttpPost("request-email-verification")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestEmailVerification([FromBody] RequestEmailCodeRequest request)
        {
            try
            {
                await _authService.RequestEmailVerificationAsync(request.Email);
                return Ok(ApiResponse<bool>.Ok(true, "Đã gửi mã xác thực email."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }

        [HttpPost("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailCodeRequest request)
        {
            try
            {
                await _authService.VerifyEmailAsync(request.Email, request.Code);
                return Ok(ApiResponse<bool>.Ok(true, "Xác thực email thành công."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<bool>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }

        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] RequestEmailCodeRequest request)
        {
            try
            {
                await _authService.RequestPasswordResetAsync(request.Email);
                return Ok(ApiResponse<bool>.Ok(true, "Nếu email tồn tại, mã đặt lại mật khẩu đã được gửi."));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            try
            {
                await _authService.ResetPasswordAsync(request);
                return Ok(ApiResponse<bool>.Ok(true, "Đã đặt lại mật khẩu."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<bool>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<IActionResult> Me()
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var user = await _authService.GetUserByIdAsync(userId);
                return Ok(ApiResponse<UserDto>.Ok(user, "Lấy thông tin người dùng thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
            }
        }

        [HttpPost("initialize-balance")]
        [Authorize]
        public async Task<IActionResult> InitializeBalance([FromBody] InitializeBalanceRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var user = await _authService.InitializeBalanceAsync(userId, request);
                return Ok(ApiResponse<UserDto>.Ok(user, "Thiết lập số dư ban đầu thành công"));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
            }
        }
    }
}
