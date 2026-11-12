using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiDailyExpense.Api.DTOs.Categories;
using AiDailyExpense.Api.DTOs.Expenses;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface IExpenseService
    {
        Task<List<CategoryDto>> GetCategoriesAsync(Guid userId);
        Task<CategoryDto> CreateCategoryAsync(Guid userId, CreateCategoryRequest request);
        Task<List<ExpenseDto>> GetExpensesAsync(Guid userId, DateTime? fromDate, DateTime? toDate, int? page = null, int? pageSize = null);
        Task<ExpenseDto?> GetExpenseByIdAsync(Guid userId, long expenseId);
        Task<ExpenseDto> CreateExpenseAsync(Guid userId, CreateExpenseRequest request);
        Task<ExpenseDto> UpdateExpenseAsync(Guid userId, long expenseId, UpdateExpenseRequest request);
        Task<bool> DeleteExpenseAsync(Guid userId, long expenseId);
    }
}
