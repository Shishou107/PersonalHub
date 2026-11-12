using System.Collections.Generic;
using AiDailyExpense.Api.DTOs.Tasks;

namespace AiDailyExpense.Api.DTOs.Ai
{
    public class AiTimetableParseResponse
    {
        public string Intent { get; set; } = "add_tasks_from_timetable";
        public string DetectedMode { get; set; } = "unknown";
        public List<TaskDto> SavedTasks { get; set; } = new();
        public int IgnoredItems { get; set; }
    }
}
