using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Ai;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.DTOs.Tasks;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public partial class AiService : IAiService
    {
        public async Task<AiTimetableParseResponse> ParseTimetableImageAsync(Guid userId, byte[] imageBytes, string mimeType)
                {
                    var provider = _configuration["Ai:Provider"] ?? "Mock";
                    var geminiApiKey = _configuration["Ai:GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
                    var openAiApiKey = _configuration["Ai:OpenAiApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        
                    var vietnamNow = GetVietnamNow();
                    var prompt = BuildTimetableImagePrompt(vietnamNow);
                    string content;
        
                    if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(geminiApiKey))
                    {
                        content = await GenerateJsonWithGeminiImageAsync(prompt, imageBytes, mimeType, geminiApiKey);
                    }
                    else if ((provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || provider.Equals("Orimise", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(openAiApiKey))
                    {
                        content = await GenerateJsonWithOpenAiImageAsync(prompt, imageBytes, mimeType, openAiApiKey);
                    }
                    else
                    {
                        throw new InvalidOperationException("Tính năng đọc ảnh thời khóa biểu cần Gemini hoặc OpenAI/Orimise API key.");
                    }
        
                    var parsed = JsonSerializer.Deserialize<AiTimetableImageParseResult>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
                    if (parsed?.Items == null || parsed.Items.Count == 0)
                    {
                        return new AiTimetableParseResponse
                        {
                            DetectedMode = parsed?.DetectedMode ?? "unknown",
                            IgnoredItems = parsed?.IgnoredItems ?? 0
                        };
                    }
        
                    var response = new AiTimetableParseResponse
                    {
                        DetectedMode = string.IsNullOrWhiteSpace(parsed.DetectedMode) ? "unknown" : parsed.DetectedMode.Trim(),
                        IgnoredItems = parsed.IgnoredItems
                    };
        
                    foreach (var item in parsed.Items)
                    {
                        if (!TryBuildTimetableTaskRequest(item, out var createReq))
                        {
                            response.IgnoredItems++;
                            continue;
                        }
        
                        createReq.Description = string.IsNullOrWhiteSpace(item.Location)
                            ? "Tạo từ ảnh thời khóa biểu bằng AI"
                            : $"Địa điểm: {item.Location.Trim()}";
                        createReq.RawText = $"Ảnh thời khóa biểu: {item.DayLabel} {item.StartAt} - {item.EndAt} {item.Title}".Trim();
        
                        var task = await _taskService.CreateTaskAsync(userId, createReq);
                        response.SavedTasks.Add(task);
                    }
        
                    return response;
                }

        private string BuildTimetableImagePrompt(DateTime vietnamNow)
                {
                    var weekStart = vietnamNow.Date.AddDays(-(((int)vietnamNow.DayOfWeek + 6) % 7));
                    var weekEnd = weekStart.AddDays(6);
        
                    return $$"""
        Bạn là bộ đọc ảnh thời khóa biểu tiếng Việt.
        
        Nhiệm vụ: đọc ảnh thời khóa biểu theo ngày hoặc theo tuần, trích xuất từng dòng có ngày/giờ/nội dung và trả JSON hợp lệ.
        
        Mốc thời gian hiện tại ở Việt Nam: {{vietnamNow:yyyy-MM-dd HH:mm}}.
        Tuần hiện tại dùng cho thời khóa biểu dạng tuần: từ {{weekStart:yyyy-MM-dd}} đến {{weekEnd:yyyy-MM-dd}}.
        
        Quy tắc:
        - Chỉ lấy các mục học/làm/thi/họp/công việc có nội dung và giờ rõ ràng.
        - Nếu ảnh có ngày cụ thể, dùng đúng ngày trong ảnh.
        - Nếu ảnh chỉ có thứ trong tuần, quy đổi sang ngày của tuần hiện tại ở Việt Nam.
        - Nếu chỉ có giờ bắt đầu mà không có giờ kết thúc, endAt = startAt + 60 phút.
        - Nếu không chắc ngày hoặc giờ thì bỏ qua mục đó, không đoán bừa.
        - title là tên môn học/nội dung công việc ngắn gọn.
        - location có thể rỗng nếu ảnh không ghi phòng/lớp/địa điểm.
        - startAt và endAt phải là chuỗi ISO không kèm timezone, ví dụ "2026-06-03T07:00:00".
        - detectedMode chỉ được là "day", "week" hoặc "unknown".
        - Chỉ trả JSON, không markdown, không giải thích.
        
        JSON cần trả:
        {
          "detectedMode": "week",
          "ignoredItems": 0,
          "items": [
            {
              "dayLabel": "Thứ 2",
              "title": "Toán",
              "location": "Phòng A1",
              "startAt": "2026-06-01T07:00:00",
              "endAt": "2026-06-01T08:30:00"
            }
          ]
        }
        """;
                }

        private bool TryBuildTimetableTaskRequest(AiTimetableImageItem item, out CreateTaskRequest request)
                {
                    request = new CreateTaskRequest();
        
                    if (string.IsNullOrWhiteSpace(item.Title) ||
                        string.IsNullOrWhiteSpace(item.StartAt) ||
                        !DateTime.TryParse(item.StartAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startAt))
                    {
                        return false;
                    }
        
                    DateTime endAt;
                    if (string.IsNullOrWhiteSpace(item.EndAt) ||
                        !DateTime.TryParse(item.EndAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out endAt))
                    {
                        endAt = startAt.AddHours(1);
                    }
        
                    if (endAt <= startAt)
                    {
                        endAt = startAt.AddHours(1);
                    }
        
                    var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    var startAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startAt, DateTimeKind.Unspecified), zone);
                    var endAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endAt, DateTimeKind.Unspecified), zone);
        
                    request = new CreateTaskRequest
                    {
                        Title = CleanTitle(item.Title),
                        StartAt = startAtUtc,
                        EndAt = endAtUtc,
                        DueAt = endAtUtc,
                        Priority = "Normal",
                        RepeatType = "None",
                        ReminderBeforeMinutes = 15
                    };
        
                    return true;
                }

        private class AiTimetableImageParseResult
                {
                    public string DetectedMode { get; set; } = "unknown";
                    public int IgnoredItems { get; set; }
                    public List<AiTimetableImageItem> Items { get; set; } = new();
                }

        private class AiTimetableImageItem
                {
                    public string DayLabel { get; set; } = string.Empty;
                    public string Title { get; set; } = string.Empty;
                    public string Location { get; set; } = string.Empty;
                    public string StartAt { get; set; } = string.Empty;
                    public string EndAt { get; set; } = string.Empty;
                }
    }
}
