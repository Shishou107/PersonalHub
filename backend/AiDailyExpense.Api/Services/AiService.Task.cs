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
        private string DetermineIntent(string text)
                {
                    var textLower = text.ToLowerInvariant();
                    var normalized = RemoveDiacritics(textLower);
                    var hasMoney = MoneyParser.Parse(text) > 0;
                    var hasDateCue =
                        Regex.IsMatch(normalized, @"\b(ngay\s*)?\d{1,2}[/-]\d{1,2}([/-]\d{2,4})?\b", RegexOptions.IgnoreCase) ||
                        Regex.IsMatch(normalized, @"\b(hom nay|ngay mai|mai)\b", RegexOptions.IgnoreCase);
        
                    var taskKeywords = new[]
                    {
                        "ghi chu", "nho", "nhac", "mai", "ngay mai", "hom nay", "lich", "cong viec",
                        "task", "can", "phai", "hen", "deadline", "hoc", "lam", "nop", "hop", "thi"
                    };
                    var completedExpensePhrases = new[]
                    {
                        "da tra", "vua tra", "moi tra", "da thanh toan", "vua thanh toan",
                        "da mua", "vua mua", "moi mua", "da an", "da uong", "da do xang"
                    };
        
                    var hasTaskCue = hasDateCue || taskKeywords.Any(k => normalized.Contains(k));
                    var looksCompletedExpense = completedExpensePhrases.Any(k => normalized.Contains(k));
        
                    if (hasTaskCue && !looksCompletedExpense)
                    {
                        return "add_task";
                    }
        
                    var reportKeywords = new[] { "bao cao", "thong ke", "chi tieu thang", "tong hop" };
                    if (reportKeywords.Any(k => normalized.Contains(k)))
                    {
                        return "query_report";
                    }
        
                    var expenseKeywords = new[]
                    {
                        "an", "uong", "pho", "ca phe", "cafe", "com", "tien", "tieu", "chi",
                        "mua", "do xang", "grab", "xe", "thanh toan", "tra"
                    };
                    if (hasMoney && expenseKeywords.Any(k => normalized.Contains(k)))
                    {
                        return "add_expense";
                    }
        
                    if (hasMoney && !hasTaskCue)
                    {
                        return "add_expense";
                    }
        
                    return "unknown";
                }

        private (string Title, DateTime? StartAt, DateTime? EndAt, DateTime? DueAt, int? ReminderBeforeMinutes) ParseTaskDetailsForCalendar(string text)
                {
                    var normalized = RemoveDiacritics(text.ToLowerInvariant());
                    var title = text;
                    var vietnamNow = GetVietnamNow();
                    var today = vietnamNow.Date;
        
                    var taskDate = today;
                    var dateMatch = Regex.Match(normalized, @"\b(?:ngay\s*)?(?<day>\d{1,2})[/-](?<month>\d{1,2})(?:[/-](?<year>\d{2,4}))?\b");
                    if (dateMatch.Success)
                    {
                        var day = int.Parse(dateMatch.Groups["day"].Value);
                        var month = int.Parse(dateMatch.Groups["month"].Value);
                        var year = vietnamNow.Year;
                        if (dateMatch.Groups["year"].Success)
                        {
                            year = int.Parse(dateMatch.Groups["year"].Value);
                            if (year < 100) year += 2000;
                        }
        
                        if (day >= 1 && day <= 31 && month >= 1 && month <= 12)
                        {
                            taskDate = new DateTime(year, month, day);
                        }
                    }
                    else if (Regex.IsMatch(normalized, @"\b(ngay mai|mai)\b"))
                    {
                        taskDate = today.AddDays(1);
                    }
                    else if (Regex.IsMatch(normalized, @"\b(hom nay)\b"))
                    {
                        taskDate = today;
                    }
        
                    var hour = 9;
                    var minute = 0;
                    var timeMatch = Regex.Match(normalized, @"\b(?<hour>\d{1,2})(?::(?<minute>\d{2})|\s*h(?:\s*(?<minute2>\d{2}))?|\s*gio)\b");
                    if (timeMatch.Success && int.TryParse(timeMatch.Groups["hour"].Value, out var parsedHour))
                    {
                        hour = Math.Clamp(parsedHour, 0, 23);
                        var minuteText = timeMatch.Groups["minute"].Success
                            ? timeMatch.Groups["minute"].Value
                            : timeMatch.Groups["minute2"].Value;
                        if (!string.IsNullOrWhiteSpace(minuteText) && int.TryParse(minuteText, out var parsedMinute))
                        {
                            minute = Math.Clamp(parsedMinute, 0, 59);
                        }
                    }
        
                    var endAt = taskDate.AddHours(hour).AddMinutes(minute);
                    if (endAt <= vietnamNow && !dateMatch.Success && !Regex.IsMatch(normalized, @"\b(hom nay)\b"))
                    {
                        endAt = endAt.AddDays(1);
                    }
                    var startAt = vietnamNow;
        
                    int? reminderMins = null;
                    var reminderMatch = Regex.Match(normalized, @"nhac\s+(?:[a-z]+\s+)?truoc\s*(\d+)\s*(phut|ph|p|gio|g|h|ngay|n)\b");
                    if (reminderMatch.Success && int.TryParse(reminderMatch.Groups[1].Value, out var val))
                    {
                        var unit = reminderMatch.Groups[2].Value;
                        if (unit == "phut" || unit == "ph" || unit == "p") reminderMins = val;
                        else if (unit == "gio" || unit == "g" || unit == "h") reminderMins = val * 60;
                        else if (unit == "ngay" || unit == "n") reminderMins = val * 1440;
                    }
        
                    // Remove patterns from title - do this in descending order of specificity (longest patterns first)
                    title = Regex.Replace(title, @"\b(?:và\s+)?(?:báo\s+)?nhắc\s+(?:\p{L}+\s+)?trước\s*\d+\s*(phút|ph|p|giờ|g|h|ngày|n)\b", "", RegexOptions.IgnoreCase).Trim();
                    title = Regex.Replace(title, @"\b(?:ngày\s*)?\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?\b", "", RegexOptions.IgnoreCase).Trim();
                    title = Regex.Replace(title, @"\b\d{1,2}(?::\d{2}|\s*h(?:\s*\d{2})?|\s*giờ|\s*gio)\b", "", RegexOptions.IgnoreCase).Trim();
                    title = Regex.Replace(title, @"\b(ghi chú|ghi chu|nhớ|nho|nhắc|nhac|để cho tôi|de cho toi|ngày mai|ngay mai|mai|hôm nay|hom nay|cần|can|phải|phai|thêm vào công việc cho tôi|lát nữa|tôi sẽ)\b", "", RegexOptions.IgnoreCase).Trim();
                    title = Regex.Replace(title, @"\s+", " ").Trim(' ', '.', '-', ':');
        
                    if (string.IsNullOrEmpty(title))
                    {
                        title = "Công việc mới";
                    }
        
                    title = char.ToUpper(title[0]) + title.Substring(1);
        
                    var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    var startAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startAt, DateTimeKind.Unspecified), zone);
                    var endAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endAt, DateTimeKind.Unspecified), zone);
        
                    return (title, startAtUtc, endAtUtc, endAtUtc, reminderMins);
                }

        private (string Title, DateTime? DueAt, int? ReminderBeforeMinutes) ParseTaskDetails(string text)
                {
                    var textLower = text.ToLower();
                    var title = text;
        
                    DateTime? dueAt = null;
                    if (textLower.Contains("ngày mai"))
                    {
                        dueAt = DateTime.UtcNow.AddDays(1).Date;
                    }
                    else if (textLower.Contains("hôm nay"))
                    {
                        dueAt = DateTime.UtcNow.Date;
                    }
        
                    if (dueAt.HasValue)
                    {
                        var timeMatch = Regex.Match(textLower, @"(\d{1,2})\s*(?:giờ|h|:00)");
                        if (timeMatch.Success && int.TryParse(timeMatch.Groups[1].Value, out var hour))
                        {
                            dueAt = dueAt.Value.AddHours(hour);
                        }
                        else
                        {
                            dueAt = dueAt.Value.AddHours(9);
                        }
                    }
        
                    int? reminderMins = null;
                    var reminderMatch = Regex.Match(textLower, @"nhắc trước\s*(\d+)\s*(phút|giờ|ngày)");
                    if (reminderMatch.Success && int.TryParse(reminderMatch.Groups[1].Value, out var val))
                    {
                        var unit = reminderMatch.Groups[2].Value;
                        if (unit == "phút") reminderMins = val;
                        else if (unit == "giờ") reminderMins = val * 60;
                        else if (unit == "ngày") reminderMins = val * 1440;
                    }
        
                    title = Regex.Replace(title, @"\b(ngày mai|hôm nay|nhắc trước\s*\d+\s*(phút|giờ|ngày))\b", "", RegexOptions.IgnoreCase).Trim();
                    title = Regex.Replace(title, @"\b\d{1,2}\s*(giờ|h)\b", "", RegexOptions.IgnoreCase).Trim();
                    title = Regex.Replace(title, @"\s+", " ");
        
                    if (string.IsNullOrEmpty(title))
                    {
                        title = "Công việc mới";
                    }
        
                    title = char.ToUpper(title[0]) + title.Substring(1);
        
                    return (title, dueAt, reminderMins);
                }

        private async Task<(string Title, DateTime? StartAt, DateTime? EndAt, DateTime? DueAt, int? ReminderBeforeMinutes)> AnalyzeTaskAsync(string text)
                {
                    var provider = _configuration["Ai:Provider"] ?? "Mock";
                    var geminiApiKey = _configuration["Ai:GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
                    var openAiApiKey = _configuration["Ai:OpenAiApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        
                    try
                    {
                        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(geminiApiKey))
                        {
                            return await AnalyzeTaskWithGeminiAsync(text, geminiApiKey);
                        }
        
                        if ((provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || provider.Equals("Orimise", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(openAiApiKey))
                        {
                            return await AnalyzeTaskWithOpenAiAsync(text, openAiApiKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Đã hết token, vui lòng liên hệ admin!", ex);
                    }
        
                    throw new InvalidOperationException("Đã hết token, vui lòng liên hệ admin!");
                }

        private async Task<(string Title, DateTime? StartAt, DateTime? EndAt, DateTime? DueAt, int? ReminderBeforeMinutes)> AnalyzeTaskWithGeminiAsync(string text, string apiKey)
                {
                    var vietnamNow = GetVietnamNow();
                    var prompt = BuildTaskPrompt(text, vietnamNow);
                    var content = await GenerateJsonWithGeminiAsync(prompt, apiKey);
        
                    var parsed = JsonSerializer.Deserialize<AiParsedTaskResult>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
                    if (parsed == null || string.IsNullOrWhiteSpace(parsed.Title))
                    {
                        throw new InvalidOperationException("Không thể giải mã JSON hoặc tiêu đề trống.");
                    }
        
                    var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        
                    DateTime? startAtUtc = null;
                    if (DateTime.TryParse(parsed.StartAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startAt))
                    {
                        startAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startAt, DateTimeKind.Unspecified), zone);
                    }
        
                    DateTime? endAtUtc = null;
                    if (DateTime.TryParse(parsed.EndAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endAt))
                    {
                        endAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endAt, DateTimeKind.Unspecified), zone);
                    }
        
                    DateTime? dueAtUtc = null;
                    if (DateTime.TryParse(parsed.DueAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueAt))
                    {
                        dueAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dueAt, DateTimeKind.Unspecified), zone);
                    }
        
                    return (parsed.Title.Trim(), startAtUtc, endAtUtc, dueAtUtc, parsed.ReminderBeforeMinutes);
                }

        private async Task<(string Title, DateTime? StartAt, DateTime? EndAt, DateTime? DueAt, int? ReminderBeforeMinutes)> AnalyzeTaskWithOpenAiAsync(string text, string apiKey)
                {
                    var vietnamNow = GetVietnamNow();
                    var prompt = BuildTaskPrompt(text, vietnamNow);
                    var content = await GenerateJsonWithOpenAiAsync(prompt, apiKey);
        
                    var parsed = JsonSerializer.Deserialize<AiParsedTaskResult>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
                    if (parsed == null || string.IsNullOrWhiteSpace(parsed.Title))
                    {
                        throw new InvalidOperationException("Không thể giải mã JSON hoặc tiêu đề trống.");
                    }
        
                    var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        
                    DateTime? startAtUtc = null;
                    if (DateTime.TryParse(parsed.StartAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startAt))
                    {
                        startAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startAt, DateTimeKind.Unspecified), zone);
                    }
        
                    DateTime? endAtUtc = null;
                    if (DateTime.TryParse(parsed.EndAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endAt))
                    {
                        endAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endAt, DateTimeKind.Unspecified), zone);
                    }
        
                    DateTime? dueAtUtc = null;
                    if (DateTime.TryParse(parsed.DueAt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueAt))
                    {
                        dueAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dueAt, DateTimeKind.Unspecified), zone);
                    }
        
                    return (parsed.Title.Trim(), startAtUtc, endAtUtc, dueAtUtc, parsed.ReminderBeforeMinutes);
                }

        private string BuildTaskPrompt(string text, DateTime vietnamNow)
                {
                    return $$"""
        Bạn là bộ phân tích công việc và nhắc nhở bằng tiếng Việt.
        
        Nhiệm vụ: Hãy đọc câu người dùng nhập, phân tích nội dung, thời gian và trả về JSON hợp lệ.
        
        Mốc thời gian hiện tại ở Việt Nam: {{vietnamNow:yyyy-MM-dd HH:mm}} (Thứ {{GetVietnameseDayOfWeek(vietnamNow.DayOfWeek)}}).
        
        Câu nhập:
        "{{text}}"
        
        Quy tắc phân tích:
        1. title: Tiêu đề công việc ngắn gọn, tự động suy luận ra nhiệm vụ/hành động chính trong câu và đặt tên (ví dụ: "Đi ngủ", "Đi học", "Đi chợ", "Họp nhóm", "Đóng tiền nhà").
           Bắt buộc phải loại bỏ hoàn toàn các từ ngữ mang tính chất câu lệnh hoặc từ thừa như "thêm vào công việc cho tôi", "nhắc tôi", "nhớ nhắc tôi", "lát nữa", "tôi sẽ", "tôi muốn", "hẹn", "đặt lịch", v.v.
           Bắt buộc giữ nguyên dấu tiếng Việt (diacritics), không được tự ý bỏ dấu hay chuyển thành không dấu.
        2. startAt: Thời gian bắt đầu là thời gian thực tại Việt Nam lúc tạo task (sử dụng mốc thời gian hiện tại: {{vietnamNow:yyyy-MM-ddTHH:mm:ss}}).
        3. endAt: Thời gian hạn chót/kết thúc công việc (chính là mốc thời gian người dùng muốn thực hiện/hoàn thành trong câu, ví dụ "1:56 đi ngủ" -> 01:56:00).
           - Định dạng ISO 8601 không kèm múi giờ (Ví dụ: "2026-06-04T01:56:00").
           - Nếu mốc thời gian này đã qua so với hiện tại (ví dụ hiện tại là 09:32 sáng mà hẹn lúc 1:56 sáng/chiều đã qua) thì tự động chuyển sang ngày tiếp theo (ngày mai) hoặc chiều tối phù hợp.
        4. dueAt: Hạn chót công việc. Mặc định bằng endAt.
        5. reminderBeforeMinutes: Số phút nhắc trước.
           - Nếu người dùng yêu cầu nhắc trước (ví dụ: "nhắc tôi trước 8 phút", "báo trước 10p"), trích xuất số phút đó (ví dụ: 8 hoặc 10).
           - Nếu không có yêu cầu nhắc nhở cụ thể, trả về null.
        
        Chỉ trả về JSON, không có định dạng markdown, không giải thích gì thêm.
        
        JSON mẫu cần trả:
        {
          "title": "Đi ngủ",
          "startAt": "{{vietnamNow:yyyy-MM-ddTHH:mm:ss}}",
          "endAt": "2026-06-04T01:56:00",
          "dueAt": "2026-06-04T01:56:00",
          "reminderBeforeMinutes": null
        }
        """;
                }

        private string GetVietnameseDayOfWeek(DayOfWeek day)
                {
                    return day switch
                    {
                        DayOfWeek.Sunday => "Chủ Nhật",
                        DayOfWeek.Monday => "Hai",
                        DayOfWeek.Tuesday => "Ba",
                        DayOfWeek.Wednesday => "Tư",
                        DayOfWeek.Thursday => "Năm",
                        DayOfWeek.Friday => "Sáu",
                        DayOfWeek.Saturday => "Bảy",
                        _ => ""
                    };
                }

        private class AiParsedTaskResult
                {
                    public string Title { get; set; } = string.Empty;
                    public string? StartAt { get; set; }
                    public string? EndAt { get; set; }
                    public string? DueAt { get; set; }
                    public int? ReminderBeforeMinutes { get; set; }
                    public string? TaskType { get; set; }
                    public string? Location { get; set; }
                    public string? Description { get; set; }
                }

        private List<AiParsedTaskResult> BuildTaskItemsFromParsedOrLocal(UnifiedAiParseResult parsed, string rawText)
                {
                    var items = new List<AiParsedTaskResult>();
                    if (parsed.Tasks != null && parsed.Tasks.Count > 0)
                    {
                        items.AddRange(parsed.Tasks.Where(t => !string.IsNullOrWhiteSpace(t.Title)));
                    }
                    else if (parsed.Task != null && !string.IsNullOrWhiteSpace(parsed.Task.Title))
                    {
                        items.Add(parsed.Task);
                    }
        
                    var localItems = ParseTaskScheduleItemsLocally(rawText);
                    var normalizedRawText = RemoveDiacritics(rawText.ToLowerInvariant());
                    var hasExplicitDate = HasExplicitTaskDate(normalizedRawText);
                    var hasExplicitTime = HasExplicitTaskTime(rawText);
                    var shouldExpandRemainingWeek = ShouldExpandRemainingWeek(normalizedRawText);
                    if (items.Count == 0)
                    {
                        return localItems;
                    }
        
                    if (shouldExpandRemainingWeek && localItems.Count > 0)
                    {
                        return localItems;
                    }
        
                    if (localItems.Count > 0 && items.Count == localItems.Count)
                    {
                        return MergeParsedTitlesWithLocalTimes(items, localItems);
                    }
        
                    if (localItems.Count == 1 && items.Count >= 1 && (hasExplicitDate || hasExplicitTime))
                    {
                        return MergeParsedTitlesWithLocalTimes(items.Take(1).ToList(), localItems);
                    }
        
                    if (localItems.Count > 1 || (localItems.Count == 1 && (hasExplicitDate || hasExplicitTime)))
                    {
                        return localItems;
                    }
        
                    return items;
                }

        private List<AiParsedTaskResult> MergeParsedTitlesWithLocalTimes(List<AiParsedTaskResult> parsedItems, List<AiParsedTaskResult> localItems)
                {
                    for (var i = 0; i < localItems.Count && i < parsedItems.Count; i++)
                    {
                        var parsed = parsedItems[i];
                        if (!string.IsNullOrWhiteSpace(parsed.Title) && IsMeaningfulTaskPart(parsed.Title))
                        {
                            localItems[i].Title = parsed.Title.Trim();
                        }
        
                        if (!string.IsNullOrWhiteSpace(parsed.TaskType))
                        {
                            localItems[i].TaskType = parsed.TaskType;
                        }
        
                        if (!string.IsNullOrWhiteSpace(parsed.Location))
                        {
                            localItems[i].Location = parsed.Location;
                        }
        
                        if (!string.IsNullOrWhiteSpace(parsed.Description))
                        {
                            localItems[i].Description = parsed.Description;
                        }
        
                        localItems[i].ReminderBeforeMinutes ??= parsed.ReminderBeforeMinutes;
                    }
        
                    return localItems;
                }

        private CreateTaskRequest BuildTaskCreateRequest(AiParsedTaskResult taskItem, string rawText)
                {
                    var vietnamNow = GetVietnamNow();
                    var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    var normalizedRawText = RemoveDiacritics(rawText.ToLowerInvariant());
                    var forcedDate = HasExplicitTaskDate(normalizedRawText)
                        ? ParseMentionedTaskDate(normalizedRawText, vietnamNow)
                        : (DateTime?)null;
                    var hasExplicitTime = HasExplicitTaskTime(rawText);
                    var shouldExpandRemainingWeek = ShouldExpandRemainingWeek(normalizedRawText);
                    var isSchedule = string.Equals(taskItem.TaskType, "Schedule", StringComparison.OrdinalIgnoreCase);
        
                    DateTime startAtLocal;
                    DateTime endAtLocal;
                    DateTime dueAtLocal;
        
                    if (isSchedule)
                    {
                        startAtLocal = TryParseLocalDateTime(taskItem.StartAt, out var parsedStart)
                            ? parsedStart
                            : hasExplicitTime
                                ? vietnamNow
                                : GetAllDayStart(forcedDate?.Date ?? vietnamNow.Date);
                        endAtLocal = TryParseLocalDateTime(taskItem.EndAt, out var parsedEnd)
                            ? parsedEnd
                            : hasExplicitTime
                                ? startAtLocal.Add(DefaultScheduleDuration)
                                : GetAllDayEnd(startAtLocal.Date);
        
                        if (endAtLocal <= startAtLocal)
                        {
                            endAtLocal = startAtLocal.Add(DefaultScheduleDuration);
                        }
        
                        dueAtLocal = TryParseLocalDateTime(taskItem.DueAt, out var parsedDue)
                            ? parsedDue
                            : endAtLocal;
        
                        if (forcedDate.HasValue)
                        {
                            var duration = endAtLocal > startAtLocal
                                ? endAtLocal - startAtLocal
                                : DefaultScheduleDuration;
        
                            startAtLocal = forcedDate.Value.Date.Add(startAtLocal.TimeOfDay);
                            endAtLocal = forcedDate.Value.Date.Add(endAtLocal.TimeOfDay);
        
                            if (endAtLocal <= startAtLocal)
                            {
                                endAtLocal = startAtLocal.Add(duration);
                            }
        
                            dueAtLocal = forcedDate.Value.Date.Add(dueAtLocal.TimeOfDay);
                            if (dueAtLocal < startAtLocal)
                            {
                                dueAtLocal = endAtLocal;
                            }
                        }
                    }
                    else
                    {
                        var hasParsedStart = TryParseLocalDateTime(taskItem.StartAt, out var parsedTaskStart);
                        startAtLocal = !hasExplicitTime && hasParsedStart
                            ? parsedTaskStart
                            : hasExplicitTime
                            ? vietnamNow
                            : GetAllDayStart(forcedDate?.Date ?? vietnamNow.Date);
                        if (TryParseLocalDateTime(taskItem.DueAt, out var parsedDue))
                        {
                            dueAtLocal = parsedDue;
                        }
                        else if (TryParseLocalDateTime(taskItem.EndAt, out var parsedEnd))
                        {
                            dueAtLocal = parsedEnd;
                        }
                        else if (forcedDate.HasValue)
                        {
                            dueAtLocal = GetAllDayEnd(forcedDate.Value.Date);
                        }
                        else
                        {
                            dueAtLocal = hasExplicitTime
                                ? startAtLocal.AddHours(1)
                                : GetAllDayEnd(startAtLocal.Date);
                        }
        
                        if (forcedDate.HasValue && !shouldExpandRemainingWeek)
                        {
                            dueAtLocal = forcedDate.Value.Date.Add(dueAtLocal.TimeOfDay);
                        }
        
                        while (dueAtLocal <= startAtLocal)
                        {
                            dueAtLocal = dueAtLocal.AddDays(1);
                        }
        
                        endAtLocal = dueAtLocal;
                    }
        
                    var startAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startAtLocal, DateTimeKind.Unspecified), zone);
                    var endAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endAtLocal, DateTimeKind.Unspecified), zone);
                    var dueAtUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(dueAtLocal, DateTimeKind.Unspecified), zone);
        
                    return new CreateTaskRequest
                    {
                        Title = FinalizeTaskTitle(taskItem.Title, rawText),
                        Description = string.IsNullOrWhiteSpace(taskItem.Description) ? null : taskItem.Description.Trim(),
                        StartAt = startAtUtc,
                        EndAt = endAtUtc,
                        DueAt = dueAtUtc,
                        Priority = "Normal",
                        RepeatType = "None",
                        ReminderBeforeMinutes = taskItem.ReminderBeforeMinutes,
                        TaskType = isSchedule ? "Schedule" : "Task",
                        Location = taskItem.Location?.Trim(),
                        RawText = rawText
                    };
                }

        private bool TryParseLocalDateTime(string? value, out DateTime dateTime)
                {
                    if (!string.IsNullOrWhiteSpace(value) &&
                        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out dateTime))
                    {
                        return true;
                    }
        
                    dateTime = default;
                    return false;
                }

        private List<AiParsedTaskResult> ParseTaskScheduleItemsLocally(string text)
                {
                    var results = new List<AiParsedTaskResult>();
                    var vietnamNow = GetVietnamNow();
                    var normalized = RemoveDiacritics(text.ToLowerInvariant());
                    var taskDate = ParseMentionedTaskDate(normalized, vietnamNow);
                    var hasExplicitDate = HasExplicitTaskDate(normalized);
                    var reminderMins = ParseReminderMinutes(normalized);
                    var taskType = IsScheduleRequest(normalized) ? "Schedule" : "Task";
                    var shouldExpandRemainingWeek = ShouldExpandRemainingWeek(normalized);
        
                    var parts = ExtractTaskParts(text);
        
                    foreach (var part in parts)
                    {
                        var partNormalized = RemoveDiacritics(part.ToLowerInvariant());
                        var rangeMatch = Regex.Match(partNormalized, $@"(?:\btu\s+)?(?<start>{TaskTimePattern()})\s*(?:-|->|cho\s+toi|cho\s+den|den|toi)\s*(?<end>{TaskTimePattern()})");
                        if (rangeMatch.Success &&
                            TryParseTaskTime(rangeMatch.Groups["start"].Value, out var startTime) &&
                            TryParseTaskTime(rangeMatch.Groups["end"].Value, out var endTime))
                        {
                            var title = CleanTaskTitle(part, rangeMatch.Value);
                            if (taskType == "Schedule")
                            {
                                foreach (var date in GetScheduleDates(taskDate, startTime, vietnamNow, hasExplicitDate, shouldExpandRemainingWeek))
                                {
                                    var startAt = BuildLocalTaskDateTime(date, startTime, vietnamNow, hasExplicitDate || shouldExpandRemainingWeek);
                                    var endAt = date.Add(endTime);
                                    if (endAt <= startAt)
                                    {
                                        endAt = endAt.AddDays(1);
                                    }
        
                                    results.Add(new AiParsedTaskResult
                                    {
                                        Title = title,
                                        StartAt = ToLocalIso(startAt),
                                        EndAt = ToLocalIso(endAt),
                                        DueAt = ToLocalIso(endAt),
                                        ReminderBeforeMinutes = reminderMins,
                                        TaskType = taskType
                                    });
                                }
                            }
                            else
                            {
                                foreach (var date in GetBatchTaskDates(taskDate, endTime, vietnamNow, hasExplicitDate, shouldExpandRemainingWeek))
                                {
                                    var dueAt = BuildLocalTaskDateTime(date, endTime, vietnamNow, hasExplicitDate || shouldExpandRemainingWeek);
                                    results.Add(new AiParsedTaskResult
                                    {
                                        Title = title,
                                        StartAt = ToLocalIso(vietnamNow),
                                        EndAt = ToLocalIso(dueAt),
                                        DueAt = ToLocalIso(dueAt),
                                        ReminderBeforeMinutes = reminderMins,
                                        TaskType = taskType
                                    });
                                }
                            }
                            continue;
                        }
        
                        var singleMatch = Regex.Match(partNormalized, $@"(?:\b(?:bat dau luc|bat dau|vao luc|vao|luc|tu)\s+)?(?<time>{TaskTimePattern()})");
                        if (singleMatch.Success && TryParseTaskTime(singleMatch.Groups["time"].Value, out var singleTime))
                        {
                            var title = CleanTaskTitle(part, singleMatch.Value);
                            if (taskType == "Schedule")
                            {
                                foreach (var date in GetScheduleDates(taskDate, singleTime, vietnamNow, hasExplicitDate, shouldExpandRemainingWeek))
                                {
                                    var startAt = BuildLocalTaskDateTime(date, singleTime, vietnamNow, hasExplicitDate || shouldExpandRemainingWeek);
                                    var endAt = startAt.Add(DefaultScheduleDuration);
        
                                    results.Add(new AiParsedTaskResult
                                    {
                                        Title = title,
                                        StartAt = ToLocalIso(startAt),
                                        EndAt = ToLocalIso(endAt),
                                        DueAt = ToLocalIso(endAt),
                                        ReminderBeforeMinutes = reminderMins,
                                        TaskType = taskType
                                    });
                                }
                            }
                            else
                            {
                                foreach (var date in GetBatchTaskDates(taskDate, singleTime, vietnamNow, hasExplicitDate, shouldExpandRemainingWeek))
                                {
                                    var dueAt = BuildLocalTaskDateTime(date, singleTime, vietnamNow, hasExplicitDate || shouldExpandRemainingWeek);
                                    results.Add(new AiParsedTaskResult
                                    {
                                        Title = title,
                                        StartAt = ToLocalIso(vietnamNow),
                                        EndAt = ToLocalIso(dueAt),
                                        DueAt = ToLocalIso(dueAt),
                                        ReminderBeforeMinutes = reminderMins,
                                        TaskType = taskType
                                    });
                                }
                            }
                        }
                    }
        
                    if (results.Count == 0)
                    {
                        if (taskType == "Task" && shouldExpandRemainingWeek)
                        {
                            foreach (var date in GetBatchTaskDates(taskDate, AllDayEndTime, vietnamNow, hasExplicitDate, shouldExpandRemainingWeek))
                            {
                                var batchStartAt = GetAllDayStart(date.Date);
                                var dueAt = GetAllDayEnd(date.Date);
                                foreach (var part in parts)
                                {
                                    results.Add(new AiParsedTaskResult
                                    {
                                        Title = CleanTaskTitle(part, null),
                                        StartAt = ToLocalIso(batchStartAt),
                                        EndAt = ToLocalIso(dueAt),
                                        DueAt = ToLocalIso(dueAt),
                                        ReminderBeforeMinutes = reminderMins,
                                        TaskType = taskType
                                    });
                                }
                            }
        
                            return results;
                        }
        
                        if (taskType == "Schedule" && shouldExpandRemainingWeek)
                        {
                            foreach (var date in GetBatchTaskDates(taskDate, AllDayEndTime, vietnamNow, hasExplicitDate, shouldExpandRemainingWeek))
                            {
                                var fallbackStartAt = GetAllDayStart(date.Date);
                                var fallbackEndAt = GetAllDayEnd(date.Date);
        
                                foreach (var part in parts)
                                {
                                    results.Add(new AiParsedTaskResult
                                    {
                                        Title = CleanTaskTitle(part, null),
                                        StartAt = ToLocalIso(fallbackStartAt),
                                        EndAt = ToLocalIso(fallbackEndAt),
                                        DueAt = ToLocalIso(fallbackEndAt),
                                        ReminderBeforeMinutes = reminderMins,
                                        TaskType = taskType
                                    });
                                }
                            }
        
                            return results;
                        }
        
                        var startAt = GetAllDayStart(taskDate.Date);
                        var endAt = GetAllDayEnd(taskDate.Date);
        
                        foreach (var part in parts)
                        {
                            results.Add(new AiParsedTaskResult
                            {
                                Title = CleanTaskTitle(part, null),
                                StartAt = ToLocalIso(startAt),
                                EndAt = ToLocalIso(endAt),
                                DueAt = ToLocalIso(endAt),
                                ReminderBeforeMinutes = reminderMins,
                                TaskType = taskType
                            });
                        }
                    }
        
                    return results;
                }

        private List<string> ExtractTaskParts(string text)
                {
                    var normalized = RemoveDiacritics(text.ToLowerInvariant());
                    var listMatch = Regex.Match(normalized, @"\b(?:bao gom|gom|gom co|bao gom co|cac viec|cac cong viec|nhung viec)\b");
                    var listText = listMatch.Success && listMatch.Index + listMatch.Length < text.Length
                        ? text.Substring(listMatch.Index + listMatch.Length)
                        : text;
        
                    var roughParts = Regex.Split(listText, @"[;,]|\s+(?:v\u00E0|va|r\u1ED3i|roi|sau d\u00F3|sau do)\s+", RegexOptions.IgnoreCase)
                        .Select(p => p.Trim())
                        .Where(p => !string.IsNullOrWhiteSpace(p))
                        .ToList();
        
                    var parts = new List<string>();
                    foreach (var part in roughParts)
                    {
                        parts.AddRange(SplitAdjacentTaskActions(part));
                    }
        
                    parts = parts
                        .Select(p => p.Trim())
                        .Where(IsMeaningfulTaskPart)
                        .ToList();
        
                    return parts.Count > 0 ? parts : new List<string> { text };
                }

        private bool IsMeaningfulTaskPart(string part)
                {
                    if (string.IsNullOrWhiteSpace(part)) return false;
        
                    var cleaned = CleanTaskTitle(part, null);
                    var normalized = RemoveDiacritics(cleaned.ToLowerInvariant()).Trim();
                    return normalized is not ("them" or "moi" or "them moi" or "cong viec moi" or "lich trinh moi");
                }

        private List<string> SplitAdjacentTaskActions(string text)
                {
                    var words = Regex.Matches(text.Trim(), @"\S+").Select(m => m.Value).ToList();
                    if (words.Count <= 2)
                    {
                        return new List<string> { text.Trim() };
                    }
        
                    var normalizedWords = words
                        .Select(w => RemoveDiacritics(w.ToLowerInvariant()).Trim('.', ',', ':', ';'))
                        .ToList();
                    var actionPhrases = new[]
                    {
                        new[] { "chay", "bo" },
                        new[] { "di", "bo" },
                        new[] { "tap", "the", "duc" },
                        new[] { "hoc" },
                        new[] { "lam" },
                        new[] { "hoan", "thanh" },
                        new[] { "doc" },
                        new[] { "mua" },
                        new[] { "nop" },
                        new[] { "gui" },
                        new[] { "goi" },
                        new[] { "tra" },
                        new[] { "dong" },
                        new[] { "don" },
                        new[] { "dua" },
                        new[] { "dan" }
                    };
        
                    var starts = new List<int> { 0 };
                    for (var i = 1; i < normalizedWords.Count; i++)
                    {
                        if (actionPhrases.Any(phrase => PhraseMatches(normalizedWords, i, phrase)))
                        {
                            starts.Add(i);
                        }
                    }
        
                    if (starts.Count <= 1)
                    {
                        return new List<string> { text.Trim() };
                    }
        
                    var result = new List<string>();
                    for (var i = 0; i < starts.Count; i++)
                    {
                        var start = starts[i];
                        var end = i + 1 < starts.Count ? starts[i + 1] : words.Count;
                        var segment = string.Join(" ", words.Skip(start).Take(end - start)).Trim();
                        if (!string.IsNullOrWhiteSpace(segment))
                        {
                            result.Add(segment);
                        }
                    }
        
                    return result;
                }

        private bool PhraseMatches(List<string> words, int start, string[] phrase)
                {
                    if (start + phrase.Length > words.Count) return false;
                    for (var i = 0; i < phrase.Length; i++)
                    {
                        if (words[start + i] != phrase[i])
                        {
                            return false;
                        }
                    }
        
                    return true;
                }

        private string TaskTimePattern()
                {
                    return @"\d{1,2}\s*(?::|h|g|gio)\s*\d{0,2}(?:\s*(?:sang|trua|chieu|toi(?!\s*(?:se|can|muon|phai|nen|sap))|dem))?";
                }

        private DateTime ParseMentionedTaskDate(string normalized, DateTime vietnamNow)
                {
                    var today = vietnamNow.Date;
                    var dateMatch = Regex.Match(normalized, @"\b(?:ngay\s*)?(?<day>\d{1,2})[/-](?<month>\d{1,2})(?:[/-](?<year>\d{2,4}))?\b");
                    if (dateMatch.Success)
                    {
                        var day = int.Parse(dateMatch.Groups["day"].Value);
                        var month = int.Parse(dateMatch.Groups["month"].Value);
                        var year = vietnamNow.Year;
                        if (dateMatch.Groups["year"].Success)
                        {
                            year = int.Parse(dateMatch.Groups["year"].Value);
                            if (year < 100) year += 2000;
                        }
        
                        if (day >= 1 && day <= 31 && month >= 1 && month <= 12)
                        {
                            return new DateTime(year, month, day);
                        }
                    }
        
                    var spokenDateMatch = Regex.Match(normalized, @"\b(?:ngay\s*)?(?<day>\d{1,2})\s*thang\s*(?<month>\d{1,2})(?:\s*nam\s*(?<year>\d{2,4}))?\b");
                    if (spokenDateMatch.Success)
                    {
                        var day = int.Parse(spokenDateMatch.Groups["day"].Value);
                        var month = int.Parse(spokenDateMatch.Groups["month"].Value);
                        var hasYear = spokenDateMatch.Groups["year"].Success;
                        var year = vietnamNow.Year;
        
                        if (hasYear)
                        {
                            year = int.Parse(spokenDateMatch.Groups["year"].Value);
                            if (year < 100) year += 2000;
                        }
        
                        if (month >= 1 && month <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month))
                        {
                            var parsedDate = new DateTime(year, month, day);
                            if (!hasYear && parsedDate < today)
                            {
                                parsedDate = parsedDate.AddYears(1);
                            }
        
                            return parsedDate;
                        }
                    }
        
                    if (Regex.IsMatch(normalized, @"\b(ngay mai|mai)\b"))
                    {
                        return today.AddDays(1);
                    }
        
                    if (Regex.IsMatch(normalized, @"\b(ngay kia|ngay mot)\b"))
                    {
                        return today.AddDays(2);
                    }
        
                    var weekday = ParseVietnameseWeekday(normalized);
                    if (weekday.HasValue)
                    {
                        var days = ((int)weekday.Value - (int)vietnamNow.DayOfWeek + 7) % 7;
                        if (days == 0)
                        {
                            days = 7;
                        }
        
                        return today.AddDays(days);
                    }
        
                    if (Regex.IsMatch(normalized, @"\b(tuan sau)\b"))
                    {
                        return today.AddDays(7);
                    }
        
                    return today;
                }

        private DayOfWeek? ParseVietnameseWeekday(string normalized)
                {
                    if (Regex.IsMatch(normalized, @"\b(chu nhat|cn)\b")) return DayOfWeek.Sunday;
                    if (Regex.IsMatch(normalized, @"\b(thu\s*2|thu hai)\b")) return DayOfWeek.Monday;
                    if (Regex.IsMatch(normalized, @"\b(thu\s*3|thu ba)\b")) return DayOfWeek.Tuesday;
                    if (Regex.IsMatch(normalized, @"\b(thu\s*4|thu tu)\b")) return DayOfWeek.Wednesday;
                    if (Regex.IsMatch(normalized, @"\b(thu\s*5|thu nam)\b")) return DayOfWeek.Thursday;
                    if (Regex.IsMatch(normalized, @"\b(thu\s*6|thu sau)\b")) return DayOfWeek.Friday;
                    if (Regex.IsMatch(normalized, @"\b(thu\s*7|thu bay)\b")) return DayOfWeek.Saturday;
                    if (Regex.IsMatch(normalized, @"\b(cuoi tuan)\b")) return DayOfWeek.Saturday;
                    return null;
                }

        private bool HasExplicitTaskDate(string normalized)
                {
                    return Regex.IsMatch(normalized, @"\b(?:ngay\s*)?\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?\b|\b(?:ngay\s*)?\d{1,2}\s*thang\s*\d{1,2}(?:\s*nam\s*\d{2,4})?\b|\b(ngay mai|mai|ngay kia|ngay mot|hom nay|tuan sau|cuoi tuan|chu nhat|cn|thu\s*[2-7]|thu hai|thu ba|thu tu|thu nam|thu sau|thu bay)\b");
                }

        private bool IsScheduleRequest(string normalized)
                {
                    return normalized.Contains("lich trinh")
                        || normalized.Contains("len lich")
                        || normalized.Contains("dat lich")
                        || normalized.Contains("them lich")
                        || normalized.Contains("cho vao lich")
                        || normalized.Contains("vao lich")
                        || Regex.IsMatch(normalized, @"\bdat\s+(?:tat ca|cac|ngay|lich|hen)\b");
                }

        private bool ShouldExpandRemainingWeek(string normalized)
                {
                    return Regex.IsMatch(normalized, @"\b(?:tat ca\s+)?(?:cac\s+)?ngay\s+con\s+lai\s+trong\s+tuan\b")
                        || Regex.IsMatch(normalized, @"\bhet\s+tuan\s+nay\b");
                }

        private List<DateTime> GetScheduleDates(DateTime taskDate, TimeSpan startTime, DateTime vietnamNow, bool hasExplicitDate, bool expandRemainingWeek)
                {
                    if (!expandRemainingWeek)
                    {
                        return new List<DateTime> { taskDate };
                    }
        
                    var today = vietnamNow.Date;
                    var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7;
                    var endOfWeek = today.AddDays(daysUntilSunday);
                    var dates = new List<DateTime>();
        
                    for (var date = today; date <= endOfWeek; date = date.AddDays(1))
                    {
                        if (date.Add(startTime) > vietnamNow)
                        {
                            dates.Add(date);
                        }
                    }
        
                    return dates.Count > 0
                        ? dates
                        : new List<DateTime> { hasExplicitDate ? taskDate : today.AddDays(1) };
                }

        private List<DateTime> GetBatchTaskDates(DateTime taskDate, TimeSpan dueTime, DateTime vietnamNow, bool hasExplicitDate, bool expandRemainingWeek)
                {
                    if (!expandRemainingWeek)
                    {
                        return new List<DateTime> { taskDate };
                    }
        
                    var today = vietnamNow.Date;
                    var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)today.DayOfWeek + 7) % 7;
                    var endOfWeek = today.AddDays(daysUntilSunday);
                    var dates = new List<DateTime>();
        
                    for (var date = today; date <= endOfWeek; date = date.AddDays(1))
                    {
                        if (date.Add(dueTime) > vietnamNow)
                        {
                            dates.Add(date);
                        }
                    }
        
                    return dates.Count > 0
                        ? dates
                        : new List<DateTime> { hasExplicitDate ? taskDate : today.AddDays(1) };
                }

        private bool HasExplicitTaskTime(string text)
                {
                    var normalized = RemoveDiacritics(text.ToLowerInvariant());
                    return Regex.IsMatch(normalized, $@"\b{TaskTimePattern()}\b");
                }

        private int? ParseReminderMinutes(string normalized)
                {
                    var reminderMatch = Regex.Match(normalized, @"nhac\s+(?:[a-z]+\s+)?truoc\s*(\d+)\s*(phut|ph|p|gio|g|h|ngay|n)\b");
                    if (!reminderMatch.Success || !int.TryParse(reminderMatch.Groups[1].Value, out var val))
                    {
                        return null;
                    }
        
                    var unit = reminderMatch.Groups[2].Value;
                    if (unit == "phut" || unit == "ph" || unit == "p") return val;
                    if (unit == "gio" || unit == "g" || unit == "h") return val * 60;
                    if (unit == "ngay" || unit == "n") return val * 1440;
                    return null;
                }

        private bool TryParseTaskTime(string value, out TimeSpan time)
                {
                    var normalizedText = RemoveDiacritics(value.ToLowerInvariant());
                    var isMorning = Regex.IsMatch(normalizedText, @"\b(sang)\b");
                    var isNoon = Regex.IsMatch(normalizedText, @"\b(trua)\b");
                    var isAfternoonOrNight = Regex.IsMatch(normalizedText, @"\b(chieu|toi|dem)\b");
                    var normalized = Regex.Replace(normalizedText, @"\b(sang|trua|chieu|toi|dem)\b", "").Replace(" ", "");
                    var match = Regex.Match(normalized, @"^(?<hour>\d{1,2})(?::(?<minute>\d{1,2})|h(?<minute2>\d{0,2})|gio(?<minute3>\d{0,2})|g(?<minute4>\d{0,2}))?$");
                    if (match.Success && int.TryParse(match.Groups["hour"].Value, out var hour))
                    {
                        var minuteText = match.Groups["minute"].Success ? match.Groups["minute"].Value
                            : match.Groups["minute2"].Success ? match.Groups["minute2"].Value
                            : match.Groups["minute3"].Success ? match.Groups["minute3"].Value
                            : match.Groups["minute4"].Value;
                        var minute = 0;
                        if (!string.IsNullOrWhiteSpace(minuteText) && !int.TryParse(minuteText, out minute))
                        {
                            minute = 0;
                        }
        
                        if (isMorning && hour == 12)
                        {
                            hour = 0;
                        }
                        else if (isNoon && hour < 11)
                        {
                            hour += 12;
                        }
                        else if (isAfternoonOrNight && hour < 12)
                        {
                            hour += 12;
                        }
        
                        if (hour >= 0 && hour <= 23 && minute >= 0 && minute <= 59)
                        {
                            time = new TimeSpan(hour, minute, 0);
                            return true;
                        }
                    }
        
                    time = default;
                    return false;
                }

        private DateTime BuildLocalTaskDateTime(DateTime taskDate, TimeSpan time, DateTime vietnamNow, bool hasExplicitDate)
                {
                    var value = taskDate.Add(time);
                    if (!hasExplicitDate && value <= vietnamNow)
                    {
                        value = value.AddDays(1);
                    }
                    return value;
                }

        private string ToLocalIso(DateTime value)
                {
                    return value.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                }

        private DateTime GetAllDayStart(DateTime date)
                {
                    return date.Date.Add(AllDayStartTime);
                }

        private DateTime GetAllDayEnd(DateTime date)
                {
                    return date.Date.Add(AllDayEndTime);
                }

        private string CleanTaskTitle(string source, string? timePhrase)
                {
                    var title = source;
                    if (!string.IsNullOrWhiteSpace(timePhrase))
                    {
                        title = Regex.Replace(title, Regex.Escape(timePhrase), "", RegexOptions.IgnoreCase);
                    }
        
                    title = Regex.Replace(title, @"^\s*(?:h\u00E3y|hay|cho\s+t\u00F4i|cho\s+toi)?\s*(?:th\u00EAm|them|\u0111\u1EB7t|dat)\s+(?:v\u00E0o|vao)?\s*(?:c\u00F4ng\s+vi\u1EC7c|cong\s+viec|l\u1ECBch\s+tr\u00ECnh|lich\s+trinh)?\s*(?:cho\s+t\u00F4i|cho\s+toi)?\s*", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b(?:ngay\s*)?\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b(?:nh\u1EAFc|nhac|b\u00E1o|bao)?\s*tr\u01B0\u1EDBc\s*\d+\s*(?:ph\u00FAt|phut|ph|p|gi\u1EDD|gio|g|h|ng\u00E0y|ngay|n)\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b(?:t\u1EA5t c\u1EA3|tat ca)?\s*(?:c\u00E1c|cac)?\s*ng\u00E0y\s+c\u00F2n\s+l\u1EA1i\s+trong\s+tu\u1EA7n\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b(?:tat ca)?\s*(?:cac)?\s*ngay\s+con\s+lai\s+trong\s+tuan\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b(h\u1EBFt\s+tu\u1EA7n\s+n\u00E0y|het\s+tuan\s+nay)\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b(ng\u00E0y mai|ngay mai|mai|ng\u00E0y kia|ngay kia|ng\u00E0y m\u1ED1t|ngay mot|h\u00F4m nay|hom nay|tu\u1EA7n sau|tuan sau|cu\u1ED1i tu\u1EA7n|cuoi tuan|ch\u1EE7 nh\u1EADt|chu nhat|thu\s*[2-7]|th\u1EE9 hai|thu hai|th\u1EE9 ba|thu ba|th\u1EE9 t\u01B0|thu tu|th\u1EE9 n\u0103m|thu nam|th\u1EE9 s\u00E1u|thu sau|th\u1EE9 b\u1EA3y|thu bay)\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b(t\u00F4i|toi)\s+(s\u1EBD|s\u1EBB|se)\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b(\u0111\u1EB7t|dat|nh\u1EDB|nho|nh\u1EAFc|nhac|l\u00EAn l\u1ECBch|len lich|l\u1ECBch tr\u00ECnh|lich trinh|c\u00F4ng vi\u1EC7c|cong viec|bao g\u1ED3m|bao gom|g\u1ED3m|gom|c\u1EA7n|can|ph\u1EA3i|phai|gi\u00FAp|giup|cho)\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b(b\u1EAFt \u0111\u1EA7u l\u00FAc|bat dau luc|b\u1EAFt \u0111\u1EA7u|bat dau|v\u00E0o l\u00FAc|vao luc|v\u00E0o|vao|l\u00FAc|luc|\u0111\u1EBFn|den|d\u1EBFn|toi|t\u1EDBi)\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\b\d{1,2}\s*(?::|h|g|gio|gi\u1EDD)\s*\d{0,2}(?:\s*(?:s\u00E1ng|sang|tr\u01B0a|trua|chi\u1EC1u|chieu|t\u1ED1i|toi|\u0111\u00EAm|dem))?\b", "", RegexOptions.IgnoreCase);
                    title = Regex.Replace(title, @"\s+", " ").Trim(' ', '.', '-', ':');
        
                    return CleanTitle(string.IsNullOrWhiteSpace(title) ? "Công việc mới" : title);
                }

        private string FinalizeTaskTitle(string? title, string rawText)
                {
                    var source = string.IsNullOrWhiteSpace(title) ? rawText : title.Trim();
                    var cleaned = CleanTaskTitle(source, null);
                    cleaned = ExtractCoreTaskAction(cleaned);
                    cleaned = RemoveTrailingTimeOfDay(cleaned);
                    cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '.', '-', ':');
                    return CleanTitle(string.IsNullOrWhiteSpace(cleaned) ? "Công việc mới" : cleaned);
                }

        private string ExtractCoreTaskAction(string title)
                {
                    var result = title.Trim();
                    result = RemoveLeadingTaskSubject(result);
        
                    var normalized = RemoveDiacritics(result.ToLowerInvariant());
                    var actionMatch = Regex.Match(normalized, @"\b(chay bo|di bo|tap the duc|an sang|an trua|an toi|hoc bai|lam bai|hop|di hoc|di lam|mua|lay|nop|gui|goi|tra|dong|don|dua|dan)\b");
                    if (actionMatch.Success && actionMatch.Index > 0)
                    {
                        var words = Regex.Matches(result, @"\S+").Select(m => m.Value).ToList();
                        var normalizedWords = words.Select(w => RemoveDiacritics(w.ToLowerInvariant()).Trim('.', ',', ':', ';')).ToList();
                        var actionWords = actionMatch.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var startIndex = -1;
        
                        for (var i = 0; i <= normalizedWords.Count - actionWords.Length; i++)
                        {
                            var ok = true;
                            for (var j = 0; j < actionWords.Length; j++)
                            {
                                if (normalizedWords[i + j] != actionWords[j])
                                {
                                    ok = false;
                                    break;
                                }
                            }
        
                            if (ok)
                            {
                                startIndex = i;
                                break;
                            }
                        }
        
                        if (startIndex > 0)
                        {
                            result = string.Join(" ", words.Skip(startIndex));
                        }
                    }
        
                    result = RemoveLeadingTaskSubject(result);
                    return result;
                }

        private string RemoveTrailingTimeOfDay(string title)
                {
                    var normalized = RemoveDiacritics(title.ToLowerInvariant()).Trim();
                    if (normalized.StartsWith("an toi") || normalized.StartsWith("an sang") || normalized.StartsWith("an trua"))
                    {
                        return title;
                    }
        
                    return Regex.Replace(title, @"\s+(?:s\u00E1ng|sang|tr\u01B0a|trua|chi\u1EC1u|chieu|t\u1ED1i|toi|\u0111\u00EAm|dem)$", "", RegexOptions.IgnoreCase).Trim();
                }

        private string RemoveLeadingTaskSubject(string title)
                {
                    var result = title.Trim();
                    var patterns = new[]
                    {
                        @"^(?:cho\s+)?(?:toi|t\u00F4i|minh|m\u00ECnh)\s+",
                        @"^(?:em|anh|chi|ch\u1ECB|ban|b\u1EA1n|me|m\u1EB9|ba|bo|b\u1ED1)\s+(?:toi|t\u00F4i|minh|m\u00ECnh)\s+",
                        @"^(?:toi|t\u00F4i|minh|m\u00ECnh)\s+(?:dan|d\u1EABn|dua|d\u01B0a|ch\u1EDF|cho)\s+(?:em|anh|chi|ch\u1ECB|ban|b\u1EA1n|me|m\u1EB9|ba|bo|b\u1ED1)\s+(?:toi|t\u00F4i|minh|m\u00ECnh)\s+",
                        @"^(?:dan|d\u1EABn|dua|d\u01B0a|ch\u1EDF|cho)\s+(?:em|anh|chi|ch\u1ECB|ban|b\u1EA1n|me|m\u1EB9|ba|bo|b\u1ED1)\s+(?:toi|t\u00F4i|minh|m\u00ECnh)\s+"
                    };
        
                    var changed = true;
                    while (changed)
                    {
                        changed = false;
                        foreach (var pattern in patterns)
                        {
                            var next = Regex.Replace(result, pattern, "", RegexOptions.IgnoreCase).Trim();
                            if (next.Length != result.Length)
                            {
                                result = next;
                                changed = true;
                            }
                        }
                    }
        
                    return result;
                }

        private async Task<AiParseResponse> ParseWithGeminiAsync(Guid userId, string text, string apiKey)
                {
                    var categories = await _context.ExpenseCategories
                        .Where(c => (c.UserId == null || c.UserId == userId))
                        .OrderBy(c => c.Type)
                        .ThenBy(c => c.Name)
                        .Select(c => new { c.Name, c.Type })
                        .ToListAsync();
        
                    var categoryLines = string.Join("\n", categories.Select(c => $"- {c.Name} ({c.Type})"));
                    var vietnamNow = GetVietnamNow();
                    var prompt = BuildUnifiedAiPrompt(text, categoryLines, vietnamNow);
                    var content = await GenerateJsonWithGeminiAsync(prompt, apiKey);
        
                    var parsed = JsonSerializer.Deserialize<UnifiedAiParseResult>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
                    if (parsed == null || string.IsNullOrWhiteSpace(parsed.Intent))
                    {
                        throw new InvalidOperationException("Không thể giải mã JSON hoặc Intent trống.");
                    }
        
                    var intent = parsed.Intent.ToLower().Trim();
                    var response = new AiParseResponse { Intent = intent };
        
                    if (intent == "add_expense" && parsed.ExpenseItems != null && parsed.ExpenseItems.Count > 0)
                    {
                        var savedItems = new List<ParsedExpenseItemDto>();
                        foreach (var parsedItem in parsed.ExpenseItems)
                        {
                            var catType = string.Equals(parsedItem.CategoryType, "Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense";
                            
                            // Fallback check based on category name if categoryType is Expense but name suggests Income
                            if (catType == "Expense" && (parsedItem.CategoryName == "Lương" || parsedItem.CategoryName == "Thu nhập khác"))
                            {
                                catType = "Income";
                            }
        
                            var category = await FindCategoryByNameAndTypeAsync(userId, parsedItem.CategoryName, catType);
        
                            if (category == null)
                            {
                                var otherType = catType == "Expense" ? "Income" : "Expense";
                                category = await FindCategoryByNameAndTypeAsync(userId, parsedItem.CategoryName, otherType);
                            }
        
                            if (category == null)
                            {
                                var fallbackName = catType == "Income" ? "Thu nhập khác" : "Khác";
                                category = await FindCategoryByNameAndTypeAsync(userId, fallbackName, catType);
                            }
        
                            var createReq = new CreateExpenseRequest
                            {
                                Title = parsedItem.Title,
                                Amount = parsedItem.Amount,
                                CategoryId = category?.CategoryId,
                                SpentAt = DateTime.UtcNow,
                                PaymentMethod = NormalizeAiPaymentMethod(parsedItem.PaymentMethod),
                                Note = null,
                                RawText = text
                            };
        
                            var expenseDto = await _expenseService.CreateExpenseAsync(userId, createReq);
                            savedItems.Add(new ParsedExpenseItemDto
                            {
                                ExpenseId = expenseDto.ExpenseId,
                                Title = expenseDto.Title,
                                Amount = expenseDto.Amount,
                                CategoryName = expenseDto.CategoryName ?? "Khác",
                                CategoryType = expenseDto.CategoryType ?? "Expense",
                                PaymentMethod = expenseDto.PaymentMethod
                            });
                        }
        
                        response.SavedItems = savedItems;
                    }
                    else if (intent == "add_task")
                    {
                        var savedTasks = new List<TaskDto>();
        
                        var tasksToProcess = BuildTaskItemsFromParsedOrLocal(parsed, text);
        
                        foreach (var taskItem in tasksToProcess)
                        {
                            var createReq = BuildTaskCreateRequest(taskItem, text);
                            var taskDto = await _taskService.CreateTaskAsync(userId, createReq);
                            savedTasks.Add(taskDto);
                        }
        
                        response.SavedTasks = savedTasks;
                        if (savedTasks.Count > 0)
                        {
                            response.Task = savedTasks[0];
                        }
                    }
                    else
                    {
                        response.Intent = "unknown";
                    }
        
                    return response;
                }

        private async Task<AiParseResponse> ParseWithOpenAiAsync(Guid userId, string text, string apiKey)
                {
                    var categories = await _context.ExpenseCategories
                        .Where(c => (c.UserId == null || c.UserId == userId))
                        .OrderBy(c => c.Type)
                        .ThenBy(c => c.Name)
                        .Select(c => new { c.Name, c.Type })
                        .ToListAsync();
        
                    var categoryLines = string.Join("\n", categories.Select(c => $"- {c.Name} ({c.Type})"));
                    var vietnamNow = GetVietnamNow();
                    var prompt = BuildUnifiedAiPrompt(text, categoryLines, vietnamNow);
                    var content = await GenerateJsonWithOpenAiAsync(prompt, apiKey);
        
                    var parsed = JsonSerializer.Deserialize<UnifiedAiParseResult>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
                    if (parsed == null || string.IsNullOrWhiteSpace(parsed.Intent))
                    {
                        throw new InvalidOperationException("Không thể giải mã JSON hoặc Intent trống.");
                    }
        
                    var intent = parsed.Intent.ToLower().Trim();
                    var response = new AiParseResponse { Intent = intent };
        
                    if (intent == "add_expense" && parsed.ExpenseItems != null && parsed.ExpenseItems.Count > 0)
                    {
                        var savedItems = new List<ParsedExpenseItemDto>();
                        foreach (var parsedItem in parsed.ExpenseItems)
                        {
                            var catType = string.Equals(parsedItem.CategoryType, "Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense";
                            
                            if (catType == "Expense" && (parsedItem.CategoryName == "Lương" || parsedItem.CategoryName == "Thu nhập khác"))
                            {
                                catType = "Income";
                            }
        
                            var category = await FindCategoryByNameAndTypeAsync(userId, parsedItem.CategoryName, catType);
        
                            if (category == null)
                            {
                                var otherType = catType == "Expense" ? "Income" : "Expense";
                                category = await FindCategoryByNameAndTypeAsync(userId, parsedItem.CategoryName, otherType);
                            }
        
                            if (category == null)
                            {
                                var fallbackName = catType == "Income" ? "Thu nhập khác" : "Khác";
                                category = await FindCategoryByNameAndTypeAsync(userId, fallbackName, catType);
                            }
        
                            var createReq = new CreateExpenseRequest
                            {
                                Title = parsedItem.Title,
                                Amount = parsedItem.Amount,
                                CategoryId = category?.CategoryId,
                                SpentAt = DateTime.UtcNow,
                                PaymentMethod = NormalizeAiPaymentMethod(parsedItem.PaymentMethod),
                                Note = null,
                                RawText = text
                            };
        
                            var expenseDto = await _expenseService.CreateExpenseAsync(userId, createReq);
                            savedItems.Add(new ParsedExpenseItemDto
                            {
                                ExpenseId = expenseDto.ExpenseId,
                                Title = expenseDto.Title,
                                Amount = expenseDto.Amount,
                                CategoryName = expenseDto.CategoryName ?? "Khác",
                                CategoryType = expenseDto.CategoryType ?? "Expense",
                                PaymentMethod = expenseDto.PaymentMethod
                            });
                        }
        
                        response.SavedItems = savedItems;
                    }
                    else if (intent == "add_task")
                    {
                        var savedTasks = new List<TaskDto>();
        
                        var tasksToProcess = BuildTaskItemsFromParsedOrLocal(parsed, text);
        
                        foreach (var taskItem in tasksToProcess)
                        {
                            var createReq = BuildTaskCreateRequest(taskItem, text);
                            var taskDto = await _taskService.CreateTaskAsync(userId, createReq);
                            savedTasks.Add(taskDto);
                        }
        
                        response.SavedTasks = savedTasks;
                        if (savedTasks.Count > 0)
                        {
                            response.Task = savedTasks[0];
                        }
                    }
                    else
                    {
                        response.Intent = "unknown";
                    }
        
                    return response;
                }

        private async Task<AiParseResponse> ParseLegacyLocallyAsync(Guid userId, string text)
                {
                    var intent = DetermineIntent(text);
                    var response = new AiParseResponse { Intent = intent };
        
                    if (intent == "add_expense")
                    {
                        var parsedItems = ParseExpenseItems(text);
                        if (parsedItems.Count > 0)
                        {
                            var savedItems = new List<ParsedExpenseItemDto>();
                            foreach (var parsed in parsedItems)
                            {
                                var catType = string.Equals(parsed.CategoryType, "Income", StringComparison.OrdinalIgnoreCase) ? "Income" : "Expense";
                                var category = await FindCategoryByNameAndTypeAsync(userId, parsed.CategoryName, catType)
                                    ?? await FindCategoryByNameAndTypeAsync(userId, parsed.CategoryName, catType == "Expense" ? "Income" : "Expense");
        
                                var createReq = new CreateExpenseRequest
                                {
                                    Title = parsed.Title,
                                    Amount = parsed.Amount,
                                    CategoryId = category?.CategoryId,
                                    SpentAt = DateTime.UtcNow,
                                    PaymentMethod = NormalizeAiPaymentMethod(parsed.PaymentMethod),
                                    Note = null,
                                    RawText = text
                                };
        
                                var expenseDto = await _expenseService.CreateExpenseAsync(userId, createReq);
                                savedItems.Add(new ParsedExpenseItemDto
                                {
                                    ExpenseId = expenseDto.ExpenseId,
                                    Title = expenseDto.Title,
                                    Amount = expenseDto.Amount,
                                    CategoryName = expenseDto.CategoryName ?? "Khác",
                                    CategoryType = expenseDto.CategoryType ?? "Expense",
                                    PaymentMethod = expenseDto.PaymentMethod
                                });
                            }
        
                            response.SavedItems = savedItems;
                        }
                    }
                    else if (intent == "add_task")
                    {
                        var savedTasks = new List<TaskDto>();
                        foreach (var taskItem in ParseTaskScheduleItemsLocally(text))
                        {
                            var createReq = BuildTaskCreateRequest(taskItem, text);
                            var taskDto = await _taskService.CreateTaskAsync(userId, createReq);
                            savedTasks.Add(taskDto);
                        }
        
                        response.SavedTasks = savedTasks;
                        if (savedTasks.Count > 0)
                        {
                            response.Task = savedTasks[0];
                        }
                    }
                    else
                    {
                        response.Intent = "unknown";
                    }
        
                    return response;
                }

        private string BuildUnifiedAiPrompt(string text, string categoryLines, DateTime vietnamNow)
                {
                    var taskTimeRules = $$"""
        IMPORTANT TASK TIME RULES:
        - For add_task, always split a sentence into multiple task items when it contains multiple time blocks or ranges.
        - For add_task, if the user says "bao gom", "gom", "bao gom co", "cac viec", "cac cong viec", or lists actions joined by "va", return one task per listed action in the "tasks" array. Never collapse the list into only the first task.
        - Example: "cho toi them vao cong viec ngay mai bao gom chay bo hoc tieng Nhat va hoan thanh bai CCNA" MUST return exactly 3 tasks: "Chay bo", "Hoc tieng Nhat", "Hoan thanh bai CCNA".
        - For add_task Task, phrases like "tat ca cac ngay con lai trong tuan", "cac ngay con lai trong tuan", or "het tuan nay" mean create one Task item per remaining day from today through Sunday. If no time is given, startAt is 00:01 and dueAt/endAt is 23:59 on each day.
        - For add_task Schedule, phrases like "tat ca cac ngay con lai trong tuan", "cac ngay con lai trong tuan", or "het tuan nay" mean create one Schedule item per remaining day from today through Sunday. Skip today if the start time already passed.
        - Treat connectors "cho toi", "cho den", "den", "toi", "-", and "->" as time ranges. Example: "16:00 cho toi 23 gio" means startAt 16:00 and endAt 23:00, not a 15-minute event.
        - Date phrases like "ngay 1 thang 7", "1 thang 7", and "ngay 01/07" are explicit dates. Use that date, not today's date.
        - For add_expense, if the user mentions multiple purchases/payments/transfers, return one expenseItems element per payment. Detect paymentMethod per item: words like "chuyen khoan", "thanh toan", "quet the", "Momo", "ZaloPay", "ngan hang" mean "Transfer"; otherwise default to "Cash".
        - For taskType "Task" with no explicit time (for example "hom nay hoc bai", "ngay mai hoc bai"): startAt is 00:01:00 and endAt/dueAt is 23:59:00 on the inferred date.
        - For taskType "Task" with an explicit deadline time (for example "mai luc 17h", "han 8h", "truoc 12h"): endAt and dueAt must be that deadline on the inferred date, while startAt remains the current Vietnam creation time {{vietnamNow:yyyy-MM-ddTHH:mm:ss}}.
        - For taskType "Task" with a specific deadline time (for example "mai luc 17h", "han 8h", "truoc 12h"): endAt and dueAt must be that deadline on the inferred date, while startAt remains the current Vietnam creation time.
        - For taskType "Schedule": explicit time ranges are real schedule start/end times. Only use this behavior when the user asks for "lich trinh", "len lich", "dat lich", or clearly describes an event schedule.
        - For taskType "Schedule", reminderBeforeMinutes is always counted backward from startAt, never from endAt or dueAt. Example: 12:00-15:00 with reminderBeforeMinutes=10 must notify at 11:50.
        - For taskType "Schedule" with only one explicit time and no end time/duration, startAt is that time and endAt/dueAt is startAt plus 15 minutes. Do not create a 1-hour event unless the user says a duration or an end time.
        - Task title must be the short core action only. Never include command words, owner/subject words, time words, reminder words, or the whole user sentence.
        - Example title cleanup: "thêm vào cho tôi chạy bộ lúc 5:45" -> "Chạy bộ"; "tôi dẫn em tôi mua hồ sơ từ 8h đến 10h" -> "Mua hồ sơ"; "em tôi chạy bộ tôi nhắc trước 2 tiếng" -> "Chạy bộ".
        - If a task has an explicit time range like "7h đến 7h30 ăn sáng" or "8h đến 12h học bài": startAt is the first time, endAt/dueAt is the second time.
        - If a task has one explicit start time like "chạy bộ lúc 5:45": startAt is 05:45 on the inferred date, endAt/dueAt is startAt plus 1 hour unless the user states a duration.
        - If a Task or Schedule has no explicit time: startAt is 00:01:00 on the inferred date, endAt/dueAt is 23:59:00 on the same date.
        - Date inference is mandatory: "hôm nay" means today's Vietnam date, "ngày mai"/"mai" means tomorrow's Vietnam date, "ngày kia"/"ngày mốt" means two days after today. Never use today's date for a task that says "ngày mai"/"mai".
        - If the user says only "thêm vào công việc ngày mai chạy bộ" with no hour, return one Task titled "Chạy bộ" on tomorrow's date, startAt at 00:01 and endAt/dueAt at 23:59.
        - Example input: "ngày mai tôi sẽ chạy bộ vào lúc 5:45 và 7h đến 7h30 tôi sẽ ăn sáng và 8h đến 12h tôi sẽ học bài"
          Must return 3 tasks:
          1) title "Chạy bộ", startAt tomorrow 05:45, endAt tomorrow 06:45
          2) title "Ăn sáng", startAt tomorrow 07:00, endAt tomorrow 07:30
          3) title "Học bài", startAt tomorrow 08:00, endAt tomorrow 12:00
        - IMPORTANT: ALWAYS preserve Vietnamese accents and diacritics in titles (e.g. "Chạy bộ", "Ăn sáng", "Học bài", "Mua hồ sơ"). DO NOT strip diacritics.
        """;
                    return $$"""
        {{taskTimeRules}}
        Bạn là trợ lý ảo phân tích chi tiêu cá nhân và quản lý công việc (tiếng Việt).
        
        Nhiệm vụ: Phân tích câu nói của người dùng để xác định mục đích (intent) là ghi chép tài chính (add_expense) hay lên lịch công việc/nhắc nhở (add_task).
        
        Mốc thời gian hiện tại ở Việt Nam: {{vietnamNow:yyyy-MM-dd HH:mm}} (Thứ {{GetVietnameseDayOfWeek(vietnamNow.DayOfWeek)}}).
        
        Danh mục chi tiêu và thu nhập hiện có (dùng nếu là add_expense):
        {{categoryLines}}
        
        Câu nói của người dùng:
        "{{text}}"
        
        Quy tắc phân loại (intent):
        - "add_expense": Giao dịch tài chính (gồm cả chi tiêu và thu nhập) có số tiền cụ thể hoặc ngụ ý. Ví dụ: "bố mẹ cho tôi 50k", "được cho tiền 50k", "nhận lương 100k", "ăn phở 40k", "tiêu 50k mua xăng".
        - "add_task": Lên lịch, nhắc nhở công việc, học tập, hành động cần làm ở mốc thời gian cụ thể (Ví dụ: "ngày mai 9h họp nhóm nhắc tôi trước 8 phút", "lát nữa 1:56 tôi sẽ đi ngủ", "nhớ đi mua phở lúc 12h", hoặc câu nói chứa cả công việc và lịch trình cần phân tách).
        - "unknown": Nếu không khớp với hai loại trên.
        
        Quy tắc phân tích chi tiết:
        1. Nếu intent là "add_expense":
           - Trích xuất danh sách các giao dịch vào mảng "expenseItems".
           - Nếu câu có nhiều khoản mua/thanh toán/nhận tiền, bắt buộc trả về nhiều phần tử trong "expenseItems", mỗi khoản là một item riêng.
           - amount là số tiền VND dạng số. Ví dụ: 40k = 40000, 1.2tr = 1200000.
           - categoryName bắt buộc phải chọn một trong các danh mục hiện có ở danh sách trên. Nếu không chắc chắn, dùng "Khác" (cho chi tiêu) hoặc "Thu nhập khác" (cho thu nhập).
           - categoryType chỉ được là "Expense" (nếu là chi tiêu/tiêu tiền/mua sắm/tặng tiền cho người khác) hoặc "Income" (nếu là thu nhập/nhận tiền/được cho tiền/nhận lương/thưởng/ai đó cho mình tiền như "bố mẹ cho tôi", "bố mẹ cho 50k").
           - paymentMethod chỉ được là "Cash" (Tiền mặt) hoặc "Transfer" (Chuyển khoản/thẻ/ví điện tử).
           - Nếu câu nói có "thanh toán", "chuyển khoản", "quẹt thẻ", "thẻ", "bank", "ngân hàng", "Momo", "ZaloPay", "ví điện tử" thì paymentMethod="Transfer". Mặc định là "Cash".
           - Nếu trong cùng một câu có cả khoản tiền mặt và khoản chuyển khoản, phải xác định paymentMethod riêng cho từng item, không dùng chung một phương thức cho toàn câu.
           - title ngắn gọn, tự nhiên, không chứa số tiền. Ví dụ: "Ăn phở", "Được cho tiền", "Nhận lương".
        
        2. Nếu intent là "add_task":
           - Phân tích thông tin tất cả công việc và lịch trình tìm thấy trong câu nói vào mảng "tasks". Mảng này có thể chứa một hoặc nhiều công việc, lịch trình khác nhau.
           - Nếu câu có các cụm "bao gồm", "gồm", "gồm có", "các việc", "các công việc" hoặc liệt kê nhiều hành động nối bằng "và", bắt buộc tách thành nhiều phần tử trong "tasks". Ví dụ: "ngày mai bao gồm chạy bộ học tiếng Nhật và hoàn thành bài CCNA" phải trả về 3 task riêng: "Chạy bộ", "Học tiếng Nhật", "Hoàn thành bài CCNA".
           - Trong mỗi phần tử của mảng "tasks":
             - title: Tiêu đề công việc/lịch trình ngắn gọn (bắt buộc giữ nguyên dấu tiếng Việt diacritics), tự động suy luận ra nhiệm vụ/hành động chính trong câu và đặt tên (ví dụ: "Đi ngủ", "Đi học", "Đi chợ", "Họp nhóm", "Đóng tiền nhà").
               Bắt buộc phải loại bỏ hoàn toàn các từ ngữ mang tính chất câu lệnh hoặc từ thừa như "thêm vào công việc cho tôi", "nhắc tôi", "nhớ nhắc tôi", "lát nữa", "tôi sẽ", "tôi muốn", "hẹn", "đặt lịch", v.v.
             - OVERRIDE CHO TASK: Nếu taskType="Task" thì startAt luôn là mốc thời gian hiện tại lúc tạo task: "{{vietnamNow:yyyy-MM-ddTHH:mm:ss}}". Không bao giờ dùng giờ/ngày người dùng nói làm startAt của Task.
             - OVERRIDE CHO TASK: Với taskType="Task", mọi ngày/giờ người dùng nói là hạn chót. Nếu chỉ có ngày như "ngày mai" mà không có giờ, endAt và dueAt phải là 23:59:00 của ngày đó. Nếu có giờ cụ thể như "mai lúc 17h", endAt và dueAt là giờ đó trên ngày đã suy luận.
             - OVERRIDE CHO SCHEDULE: Chỉ khi taskType="Schedule" thì startAt/endAt mới là thời gian bắt đầu/kết thúc thật của lịch trình. Dùng Schedule khi người dùng nói "lịch trình", "lên lịch", "đặt lịch", hoặc mô tả một sự kiện có khoảng giờ rõ ràng.
             - endAt: Với Task là hạn chót; với Schedule là giờ kết thúc lịch trình.
               - Định dạng ISO 8601 không kèm múi giờ (Ví dụ: "2026-06-04T01:56:00").
               - Nếu mốc thời gian này đã qua so với hiện tại (ví dụ hiện tại là 09:32 sáng mà hẹn lúc 1:56 sáng/chiều đã qua) thì tự động chuyển sang ngày tiếp theo (ngày mai) hoặc chiều tối phù hợp.
             - dueAt: Hạn chót. Mặc định bằng endAt.
             - Quy tắc ngày bắt buộc: "hôm nay" = ngày hiện tại ở Việt Nam; "ngày mai"/"mai" = ngày kế tiếp ở Việt Nam; "ngày kia"/"ngày mốt" = sau 2 ngày. Không được trả về ngày hôm nay khi câu có "ngày mai"/"mai".
             - reminderBeforeMinutes: Số phút nhắc trước. Trích xuất nếu người dùng yêu cầu nhắc trước (Ví dụ: "nhắc trước 8 phút" -> 8, "báo trước 1 tiếng" -> 60). Nếu không yêu cầu nhắc cụ thể, trả về null.
             - taskType: Bắt buộc là "Task" hoặc "Schedule".
               - Nếu người dùng nói "thêm lịch trình...", "lịch trình...", "lên lịch...", hoặc câu có ý là lịch trình sự kiện thì đặt là "Schedule".
               - Nếu người dùng nói "thêm công việc...", "thêm nhiệm vụ...", "thêm task...", hoặc câu nói hành động thông thường thì đặt là "Task".
             - location: Địa điểm diễn ra (Ví dụ: "tại phòng họp 2", "ở quán cafe"). Chỉ trích xuất địa điểm nếu người dùng có nhắc tới, nếu không có thì trả về null.
             - description: Ghi chú hoặc mô tả bổ sung từ câu nói của người dùng. Nếu không có thì trả về null.
        
        Chỉ trả về JSON, không có định dạng markdown, không giải thích gì thêm.
        
        JSON mẫu cần trả:
        Nếu intent là "add_expense" (Ghi chép giao dịch tài chính - chi tiêu hoặc thu nhập):
        {
          "intent": "add_expense",
          "expenseItems": [
            {
              "title": "Ăn phở",
              "amount": 40000,
              "categoryName": "Ăn uống",
              "categoryType": "Expense",
              "paymentMethod": "Cash"
            },
            {
              "title": "Nhận lương",
              "amount": 10000000,
              "categoryName": "Lương",
              "categoryType": "Income",
              "paymentMethod": "Transfer"
            }
          ],
          "tasks": []
        }
        
        Nếu intent là "add_task" (Lên lịch/nhắc nhở công việc hoặc lịch trình):
        {
          "intent": "add_task",
          "expenseItems": [],
          "tasks": [
            {
              "title": "Đi ngủ",
              "startAt": "{{vietnamNow:yyyy-MM-ddTHH:mm:ss}}",
              "endAt": "2026-06-04T01:56:00",
              "dueAt": "2026-06-04T01:56:00",
              "reminderBeforeMinutes": null,
              "taskType": "Task",
              "location": null,
              "description": null
            },
            {
              "title": "Họp nhóm",
              "startAt": "{{vietnamNow.AddDays(1):yyyy-MM-dd}}T09:00:00",
              "endAt": "{{vietnamNow.AddDays(1):yyyy-MM-dd}}T10:00:00",
              "dueAt": "{{vietnamNow.AddDays(1):yyyy-MM-dd}}T10:00:00",
              "reminderBeforeMinutes": 10,
              "taskType": "Schedule",
              "location": "Phòng họp A",
              "description": "Thảo luận bài tập lớn"
            }
          ]
        }
        """;
                }
    }
}
