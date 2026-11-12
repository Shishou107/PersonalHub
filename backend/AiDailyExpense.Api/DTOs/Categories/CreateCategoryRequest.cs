using System.ComponentModel.DataAnnotations;

namespace AiDailyExpense.Api.DTOs.Categories
{
    public class CreateCategoryRequest
    {
        [Required(ErrorMessage = "Tên danh mục là bắt buộc.")]
        [StringLength(100, ErrorMessage = "Tên danh mục tối đa 100 ký tự.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Loại danh mục là bắt buộc.")]
        [RegularExpression("^(Expense|Income)$", ErrorMessage = "Loại danh mục chỉ được là Expense hoặc Income.")]
        public string Type { get; set; } = "Expense";

        public string? Icon { get; set; }
        public string? Color { get; set; }
    }
}
