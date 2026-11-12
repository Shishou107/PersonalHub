using System.Collections.Generic;
using AiDailyExpense.Api.DTOs.Tasks;

namespace AiDailyExpense.Api.DTOs.Ai
{
    public class AiParseResponse
    {
        public string Intent { get; set; } = "unknown"; // add_expense, add_task, query_report, unknown
        public List<ParsedExpenseItemDto>? SavedItems { get; set; }
        public TaskDto? Task { get; set; }
        public List<TaskDto>? SavedTasks { get; set; }
    }
}
