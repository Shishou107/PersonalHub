using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Models;

namespace AiDailyExpense.Api.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            // 1. Seed default categories if none exist
            if (!await context.ExpenseCategories.AnyAsync(c => c.UserId == null))
            {
                var defaultCategories = new List<ExpenseCategory>
                {
                    new ExpenseCategory { Name = "Ăn uống", Type = "Expense", Icon = "food", Color = "#F97316", IsDefault = true, CreatedAt = DateTime.UtcNow },
                    new ExpenseCategory { Name = "Di chuyển", Type = "Expense", Icon = "transport", Color = "#3B82F6", IsDefault = true, CreatedAt = DateTime.UtcNow },
                    new ExpenseCategory { Name = "Học tập", Type = "Expense", Icon = "book", Color = "#8B5CF6", IsDefault = true, CreatedAt = DateTime.UtcNow },
                    new ExpenseCategory { Name = "Giải trí", Type = "Expense", Icon = "game", Color = "#EC4899", IsDefault = true, CreatedAt = DateTime.UtcNow },
                    new ExpenseCategory { Name = "Tiền nhà", Type = "Expense", Icon = "home", Color = "#10B981", IsDefault = true, CreatedAt = DateTime.UtcNow },
                    new ExpenseCategory { Name = "Sức khỏe", Type = "Expense", Icon = "health", Color = "#EF4444", IsDefault = true, CreatedAt = DateTime.UtcNow },
                    new ExpenseCategory { Name = "Mua sắm", Type = "Expense", Icon = "shopping", Color = "#6366F1", IsDefault = true, CreatedAt = DateTime.UtcNow },
                    new ExpenseCategory { Name = "Khác", Type = "Expense", Icon = "other", Color = "#6B7280", IsDefault = true, CreatedAt = DateTime.UtcNow },
                    new ExpenseCategory { Name = "Lương", Type = "Income", Icon = "salary", Color = "#22C55E", IsDefault = true, CreatedAt = DateTime.UtcNow },
                    new ExpenseCategory { Name = "Thu nhập khác", Type = "Income", Icon = "income", Color = "#14B8A6", IsDefault = true, CreatedAt = DateTime.UtcNow }
                };

                context.ExpenseCategories.AddRange(defaultCategories);
                await context.SaveChangesAsync();
            }

        }
    }
}
