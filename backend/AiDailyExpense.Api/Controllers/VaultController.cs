using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Vault;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VaultController : ControllerBase
    {
        private readonly IVaultService _vaultService;
        private readonly CurrentUserHelper _currentUserHelper;

        public VaultController(IVaultService vaultService, CurrentUserHelper currentUserHelper)
        {
            _vaultService = vaultService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories()
        {
            var userId = _currentUserHelper.GetUserId();
            var result = await _vaultService.GetCategoriesAsync(userId);
            return Ok(ApiResponse<List<VaultCategoryDto>>.Ok(result));
        }

        [HttpGet("items")]
        public async Task<IActionResult> GetItems([FromQuery] string? keyword)
        {
            var userId = _currentUserHelper.GetUserId();
            var result = await _vaultService.GetItemsAsync(userId, keyword);
            return Ok(ApiResponse<List<VaultItemDto>>.Ok(result));
        }

        [HttpPost("items")]
        public async Task<IActionResult> CreateItem([FromBody] CreateVaultItemRequest request)
        {
            var userId = _currentUserHelper.GetUserId();
            var result = await _vaultService.CreateItemAsync(userId, request);
            return Ok(ApiResponse<VaultItemDto>.Ok(result, "Đã lưu tài khoản."));
        }

        [HttpPut("items/{id:long}")]
        public async Task<IActionResult> UpdateItem(long id, [FromBody] UpdateVaultItemRequest request)
        {
            var userId = _currentUserHelper.GetUserId();
            var result = await _vaultService.UpdateItemAsync(userId, id, request);
            return Ok(ApiResponse<VaultItemDto>.Ok(result, "Đã cập nhật tài khoản."));
        }

        [HttpDelete("items/{id:long}")]
        public async Task<IActionResult> DeleteItem(long id)
        {
            var userId = _currentUserHelper.GetUserId();
            var result = await _vaultService.DeleteItemAsync(userId, id);
            return Ok(ApiResponse<bool>.Ok(result, "Đã xóa tài khoản."));
        }

        [HttpPost("security/pin")]
        public async Task<IActionResult> SetPin([FromBody] SetVaultPinRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                await _vaultService.SetPinAsync(userId, request);
                return Ok(ApiResponse<bool>.Ok(true, "Đã cập nhật PIN kho tài khoản thành công."));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<bool>.Fail(ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<bool>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<bool>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<bool>.Fail("Có lỗi xảy ra khi thiết lập PIN.", new List<string> { ex.Message }));
            }
        }

        [HttpPost("security/request-code")]
        public async Task<IActionResult> RequestRevealCode()
        {
            var userId = _currentUserHelper.GetUserId();
            await _vaultService.RequestRevealCodeAsync(userId);
            return Ok(ApiResponse<bool>.Ok(true, "Đã gửi mã xác thực qua email."));
        }

        [HttpPost("items/{id:long}/reveal")]
        public async Task<IActionResult> RevealItem(long id, [FromBody] RevealVaultItemRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var result = await _vaultService.RevealItemAsync(userId, id, request);
                return Ok(ApiResponse<RevealedVaultItemDto>.Ok(result, "Đã mở khóa mật khẩu."));
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ApiResponse<RevealedVaultItemDto>.Fail(ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse<RevealedVaultItemDto>.Fail(ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ApiResponse<RevealedVaultItemDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<RevealedVaultItemDto>.Fail("Có lỗi xảy ra khi mở khóa mật khẩu.", new List<string> { ex.Message }));
            }
        }
    }
}
