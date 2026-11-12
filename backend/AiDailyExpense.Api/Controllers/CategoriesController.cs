using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiDailyExpense.Api.DTOs.Categories;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriesController : ControllerBase
    {
        private readonly IExpenseService _expenseService;
        private readonly CurrentUserHelper _currentUserHelper;

        public CategoriesController(IExpenseService expenseService, CurrentUserHelper currentUserHelper)
        {
            _expenseService = expenseService;
            _currentUserHelper = currentUserHelper;
        }

        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var categories = await _expenseService.GetCategoriesAsync(userId);
                return Ok(ApiResponse<List<CategoryDto>>.Ok(categories, "Lấy danh sách danh mục thành công"));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse<List<CategoryDto>>.Fail(ex.Message));
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
        {
            try
            {
                var userId = _currentUserHelper.GetUserId();
                var category = await _expenseService.CreateCategoryAsync(userId, request);
                return Ok(ApiResponse<CategoryDto>.Ok(category, "Tạo danh mục thành công"));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<CategoryDto>.Fail(ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse<CategoryDto>.Fail("Có lỗi xảy ra khi tạo danh mục.", new List<string> { ex.Message }));
            }
        }
    }
}
