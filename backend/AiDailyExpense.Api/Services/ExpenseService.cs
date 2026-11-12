using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Categories;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class ExpenseService : IExpenseService
    {
        private readonly AppDbContext _context;

        public ExpenseService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<CategoryDto>> GetCategoriesAsync(Guid userId)
        {
            var categories = await _context.ExpenseCategories
                .Where(c => c.UserId == null || c.UserId == userId)
                .OrderBy(c => c.IsDefault ? 0 : 1)
                .ThenBy(c => c.CategoryId)
                .Select(c => new CategoryDto
                {
                    CategoryId = c.CategoryId,
                    UserId = c.UserId,
                    Name = c.Name,
                    Type = c.Type,
                    Icon = c.Icon,
                    Color = c.Color,
                    IsDefault = c.IsDefault
                })
                .ToListAsync();

            return categories;
        }

        public async Task<CategoryDto> CreateCategoryAsync(Guid userId, CreateCategoryRequest request)
        {
            var exists = await _context.ExpenseCategories
                .AnyAsync(c => (c.UserId == userId || c.UserId == null) && c.Name.ToLower() == request.Name.Trim().ToLower() && c.Type == request.Type);

            if (exists)
            {
                throw new ArgumentException("Danh mục với tên và loại này đã tồn tại.");
            }

            var category = new ExpenseCategory
            {
                UserId = userId,
                Name = request.Name.Trim(),
                Type = request.Type,
                Icon = request.Icon,
                Color = request.Color,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.ExpenseCategories.Add(category);
            await _context.SaveChangesAsync();

            return new CategoryDto
            {
                CategoryId = category.CategoryId,
                UserId = category.UserId,
                Name = category.Name,
                Type = category.Type,
                Icon = category.Icon,
                Color = category.Color,
                IsDefault = category.IsDefault
            };
        }

        public async Task<List<ExpenseDto>> GetExpensesAsync(Guid userId, DateTime? fromDate, DateTime? toDate, int? page = null, int? pageSize = null)
        {
            var query = _context.Expenses
                .AsNoTracking()
                .Include(e => e.Category)
                .Where(e => e.UserId == userId);

            if (fromDate.HasValue || toDate.HasValue)
            {
                var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId);
                var timeZoneId = user?.TimeZone ?? "Asia/Ho_Chi_Minh";
                var tz = Helpers.DateTimeHelper.GetUserTimeZone(timeZoneId);

                if (fromDate.HasValue)
                {
                    var fromLocal = fromDate.Value.Date;
                    var fromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fromLocal, DateTimeKind.Unspecified), tz);
                    query = query.Where(e => e.SpentAt >= fromUtc);
                }

                if (toDate.HasValue)
                {
                    var toLocalNextDay = toDate.Value.Date.AddDays(1);
                    var toUtcNextDay = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(toLocalNextDay, DateTimeKind.Unspecified), tz);
                    query = query.Where(e => e.SpentAt < toUtcNextDay);
                }
            }

            query = query.OrderByDescending(e => e.SpentAt);

            if (page.HasValue && pageSize.HasValue && page.Value > 0 && pageSize.Value > 0)
            {
                query = query.Skip((page.Value - 1) * pageSize.Value).Take(pageSize.Value);
            }

            var expenses = await query
                .Select(e => new ExpenseDto
                {
                    ExpenseId = e.ExpenseId,
                    UserId = e.UserId,
                    CategoryId = e.CategoryId,
                    CategoryName = e.Category != null ? e.Category.Name : "Khác",
                    CategoryIcon = e.Category != null ? e.Category.Icon : "other",
                    CategoryColor = e.Category != null ? e.Category.Color : "#6B7280",
                    Title = e.Title,
                    Amount = e.Amount,
                    SpentAt = e.SpentAt,
                    PaymentMethod = e.PaymentMethod,
                    CategoryType = e.Category != null ? e.Category.Type : "Expense",
                    Note = e.Note,
                    RawText = e.RawText,
                    CreatedAt = e.CreatedAt,
                    UpdatedAt = e.UpdatedAt
                })
                .ToListAsync();

            return expenses;
        }

        public async Task<ExpenseDto?> GetExpenseByIdAsync(Guid userId, long expenseId)
        {
            var expense = await _context.Expenses
                .Include(e => e.Category)
                .FirstOrDefaultAsync(e => e.UserId == userId && e.ExpenseId == expenseId);

            if (expense == null) return null;

            return new ExpenseDto
            {
                ExpenseId = expense.ExpenseId,
                UserId = expense.UserId,
                CategoryId = expense.CategoryId,
                CategoryName = expense.Category != null ? expense.Category.Name : "Khác",
                CategoryIcon = expense.Category != null ? expense.Category.Icon : "other",
                CategoryColor = expense.Category != null ? expense.Category.Color : "#6B7280",
                Title = expense.Title,
                Amount = expense.Amount,
                SpentAt = expense.SpentAt,
                PaymentMethod = expense.PaymentMethod,
                CategoryType = expense.Category != null ? expense.Category.Type : "Expense",
                Note = expense.Note,
                RawText = expense.RawText,
                CreatedAt = expense.CreatedAt,
                UpdatedAt = expense.UpdatedAt
            };
        }

        public async Task<ExpenseDto> CreateExpenseAsync(Guid userId, CreateExpenseRequest request)
        {
            if (request.CategoryId.HasValue)
            {
                var catExists = await _context.ExpenseCategories.AnyAsync(c => c.CategoryId == request.CategoryId.Value && (c.UserId == null || c.UserId == userId));
                if (!catExists)
                {
                    throw new ArgumentException("Danh mục chi tiêu không tồn tại hoặc không thuộc về người dùng.");
                }
            }

            var expense = new Expense
            {
                UserId = userId,
                CategoryId = request.CategoryId,
                Title = request.Title.Trim(),
                Amount = request.Amount,
                SpentAt = request.SpentAt,
                PaymentMethod = NormalizePaymentMethod(request.PaymentMethod),
                Note = request.Note?.Trim(),
                RawText = request.RawText?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Expenses.Add(expense);
            await ApplyBalanceChangeAsync(userId, expense.CategoryId, expense.Amount, expense.PaymentMethod, true);
            await _context.SaveChangesAsync();

            // Fetch name of category to return DTO properly
            string categoryName = "Khác";
            string categoryIcon = "other";
            string categoryColor = "#6B7280";
            string categoryType = "Expense";
            if (expense.CategoryId.HasValue)
            {
                var category = await _context.ExpenseCategories.FindAsync(expense.CategoryId.Value);
                if (category != null)
                {
                    categoryName = category.Name;
                    categoryIcon = category.Icon ?? "other";
                    categoryColor = category.Color ?? "#6B7280";
                    categoryType = category.Type;
                }
            }

            return new ExpenseDto
            {
                ExpenseId = expense.ExpenseId,
                UserId = expense.UserId,
                CategoryId = expense.CategoryId,
                CategoryName = categoryName,
                CategoryIcon = categoryIcon,
                CategoryColor = categoryColor,
                Title = expense.Title,
                Amount = expense.Amount,
                SpentAt = expense.SpentAt,
                PaymentMethod = expense.PaymentMethod,
                CategoryType = categoryType,
                Note = expense.Note,
                RawText = expense.RawText,
                CreatedAt = expense.CreatedAt
            };
        }

        public async Task<ExpenseDto> UpdateExpenseAsync(Guid userId, long expenseId, UpdateExpenseRequest request)
        {
            var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.UserId == userId && e.ExpenseId == expenseId);
            if (expense == null)
            {
                throw new KeyNotFoundException("Không tìm thấy khoản chi tiêu hoặc bạn không có quyền sửa.");
            }

            if (request.CategoryId.HasValue)
            {
                var catExists = await _context.ExpenseCategories.AnyAsync(c => c.CategoryId == request.CategoryId.Value && (c.UserId == null || c.UserId == userId));
                if (!catExists)
                {
                    throw new ArgumentException("Danh mục chi tiêu không tồn tại hoặc không thuộc về người dùng.");
                }
            }

            var oldCategoryId = expense.CategoryId;
            var oldAmount = expense.Amount;
            var oldPaymentMethod = expense.PaymentMethod;

            expense.Title = request.Title.Trim();
            expense.Amount = request.Amount;
            expense.CategoryId = request.CategoryId;
            expense.SpentAt = request.SpentAt;
            expense.PaymentMethod = NormalizePaymentMethod(request.PaymentMethod);
            expense.Note = request.Note?.Trim();
            expense.UpdatedAt = DateTime.UtcNow;

            await ApplyBalanceChangeAsync(userId, oldCategoryId, oldAmount, oldPaymentMethod, false);
            await ApplyBalanceChangeAsync(userId, expense.CategoryId, expense.Amount, expense.PaymentMethod, true);
            await _context.SaveChangesAsync();

            string categoryName = "Khác";
            string categoryIcon = "other";
            string categoryColor = "#6B7280";
            string updateCategoryType = "Expense";
            if (expense.CategoryId.HasValue)
            {
                var category = await _context.ExpenseCategories.FindAsync(expense.CategoryId.Value);
                if (category != null)
                {
                    categoryName = category.Name;
                    categoryIcon = category.Icon ?? "other";
                    categoryColor = category.Color ?? "#6B7280";
                    updateCategoryType = category.Type;
                }
            }

            return new ExpenseDto
            {
                ExpenseId = expense.ExpenseId,
                UserId = expense.UserId,
                CategoryId = expense.CategoryId,
                CategoryName = categoryName,
                CategoryIcon = categoryIcon,
                CategoryColor = categoryColor,
                Title = expense.Title,
                Amount = expense.Amount,
                SpentAt = expense.SpentAt,
                PaymentMethod = expense.PaymentMethod,
                CategoryType = updateCategoryType,
                Note = expense.Note,
                RawText = expense.RawText,
                CreatedAt = expense.CreatedAt,
                UpdatedAt = expense.UpdatedAt
            };
        }

        public async Task<bool> DeleteExpenseAsync(Guid userId, long expenseId)
        {
            var expense = await _context.Expenses.FirstOrDefaultAsync(e => e.UserId == userId && e.ExpenseId == expenseId);
            if (expense == null)
            {
                return false;
            }

            // Delete corresponding bank transaction if it exists
            if (!string.IsNullOrWhiteSpace(expense.RawText))
            {
                var bankTx = await _context.BankTransactions
                    .FirstOrDefaultAsync(t => t.UserId == userId && t.RawMessage == expense.RawText);
                if (bankTx != null)
                {
                    _context.BankTransactions.Remove(bankTx);
                }
            }

            await ApplyBalanceChangeAsync(userId, expense.CategoryId, expense.Amount, expense.PaymentMethod, false);
            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();
            return true;
        }

        private static string NormalizePaymentMethod(string? paymentMethod)
        {
            return string.Equals(paymentMethod, "Transfer", StringComparison.OrdinalIgnoreCase) ? "Transfer" : "Cash";
        }

        private async Task ApplyBalanceChangeAsync(Guid userId, int? categoryId, decimal amount, string? paymentMethod, bool apply)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy người dùng.");
            }

            var categoryType = "Expense";
            if (categoryId.HasValue)
            {
                var category = await _context.ExpenseCategories
                    .FirstOrDefaultAsync(c => c.CategoryId == categoryId.Value && (c.UserId == null || c.UserId == userId));
                categoryType = category?.Type ?? "Expense";
            }

            var signedAmount = string.Equals(categoryType, "Income", StringComparison.OrdinalIgnoreCase) ? amount : -amount;
            if (!apply)
            {
                signedAmount = -signedAmount;
            }

            if (NormalizePaymentMethod(paymentMethod) == "Transfer")
            {
                user.BankBalance += signedAmount;
            }
            else
            {
                user.CashBalance += signedAmount;
            }
        }
    }
}
