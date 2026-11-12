using System;

namespace AiDailyExpense.Api.DTOs.Categories
{
    public class CategoryDto
    {
        public int CategoryId { get; set; }
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Expense"; // Expense or Income
        public string? Icon { get; set; }
        public string? Color { get; set; }
        public bool IsDefault { get; set; }
    }
}
