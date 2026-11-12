using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Tasks;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class TaskService : ITaskService
    {
        private readonly AppDbContext _context;

        public TaskService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<TaskDto>> GetTasksAsync(Guid userId, DateTime? date, string? status)
        {
            var now = DateTime.UtcNow;
            var query = _context.UserTasks.Where(t => t.UserId == userId);

            if (date.HasValue)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                var timeZoneId = user?.TimeZone ?? "Asia/Ho_Chi_Minh";
                var (startUtc, endUtc) = Helpers.DateTimeHelper.GetUtcRangeForLocalDate(date.Value, timeZoneId);
                
                query = query.Where(t => t.DueAt.HasValue && t.DueAt.Value >= startUtc && t.DueAt.Value < endUtc);
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(t => t.Status == status);
            }

            var tasks = await query
                .AsNoTracking()
                .Include(t => t.Reminders)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            return tasks.Select(t => ToTaskDto(t, now)).ToList();
        }

        public async Task<List<TaskDto>> GetTodayTasksAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            var timeZoneId = user?.TimeZone ?? "Asia/Ho_Chi_Minh";
            var tz = Helpers.DateTimeHelper.GetUserTimeZone(timeZoneId);
            
            var userLocalTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var todayLocal = userLocalTime.Date;
            
            var (todayUtc, tomorrowUtc) = Helpers.DateTimeHelper.GetUtcRangeForLocalDate(todayLocal, timeZoneId);
            var nowUtc = DateTime.UtcNow;

            // Fetch tasks due today or pending tasks with past due dates
            var tasks = await _context.UserTasks
                .AsNoTracking()
                .Include(t => t.Reminders)
                .Where(t => t.UserId == userId && 
                            ((t.DueAt.HasValue && t.DueAt.Value >= todayUtc && t.DueAt.Value < tomorrowUtc) ||
                             (t.DueAt.HasValue && t.DueAt.Value < todayUtc && t.Status == "Pending") ||
                             (!t.DueAt.HasValue && t.Status == "Pending" && t.CreatedAt >= todayUtc && t.CreatedAt < tomorrowUtc)))
                .OrderBy(t => t.Status == "Pending" ? 0 : 1)
                .ThenBy(t => t.DueAt)
                .ToListAsync();

            return tasks.Select(t => ToTaskDto(t, nowUtc)).ToList();
        }

        public async Task<TaskDto?> GetTaskByIdAsync(Guid userId, long taskId)
        {
            var now = DateTime.UtcNow;
            var task = await _context.UserTasks
                .Include(t => t.Reminders)
                .FirstOrDefaultAsync(t => t.UserId == userId && t.TaskId == taskId);

            if (task == null) return null;

            return ToTaskDto(task, now);
        }

        public async Task<TaskDto> CreateTaskAsync(Guid userId, CreateTaskRequest request)
        {
            var startAt = NormalizeIncomingTaskTime(request.StartAt);
            var endAt = NormalizeIncomingTaskTime(request.EndAt);
            var dueAt = NormalizeIncomingTaskTime(request.DueAt) ?? endAt;

            if (request.TaskType == "Schedule" && startAt.HasValue && endAt.HasValue && endAt <= startAt)
            {
                throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");
            }

            var task = new UserTask
            {
                UserId = userId,
                Title = request.Title.Trim(),
                Description = request.Description?.Trim(),
                StartAt = startAt,
                EndAt = endAt,
                DueAt = dueAt,
                Status = "Pending",
                Priority = request.Priority,
                RepeatType = request.RepeatType,
                TaskType = request.TaskType,
                Location = request.Location?.Trim(),
                RawText = request.RawText?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.UserTasks.Add(task);
            await _context.SaveChangesAsync();

            // Create reminder if requested
            var reminderReferenceTime = GetReminderReferenceTime(task);
            if (request.ReminderBeforeMinutes.HasValue && reminderReferenceTime.HasValue)
            {
                var remindTime = reminderReferenceTime.Value.AddMinutes(-request.ReminderBeforeMinutes.Value);
                var reminder = new Reminder
                {
                    UserId = userId,
                    TaskId = task.TaskId,
                    Title = task.Title,
                    Message = task.TaskType == "Schedule" 
                        ? $"Sắp đến giờ diễn ra lịch trình: {task.Title}" 
                        : $"Sắp đến giờ thực hiện công việc: {task.Title}",
                    RemindAt = remindTime,
                    IsSent = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Reminders.Add(reminder);
                await _context.SaveChangesAsync();
            }

            return new TaskDto
            {
                TaskId = task.TaskId,
                UserId = task.UserId,
                Title = task.Title,
                Description = task.Description,
                StartAt = task.StartAt,
                EndAt = task.EndAt,
                DueAt = task.DueAt,
                Status = task.Status,
                Priority = task.Priority,
                RepeatType = task.RepeatType,
                RawText = task.RawText,
                CreatedAt = task.CreatedAt,
                IsOverdue = task.TaskType == "Task" && task.DueAt.HasValue && task.DueAt.Value < DateTime.UtcNow && task.Status == "Pending",
                ReminderBeforeMinutes = request.ReminderBeforeMinutes,
                TaskType = task.TaskType,
                Location = task.Location
            };
        }

        public async Task<List<TaskDto>> CreateTasksBatchAsync(Guid userId, List<CreateTaskRequest> requests)
        {
            if (requests == null || !requests.Any())
            {
                return new List<TaskDto>();
            }

            var results = new List<TaskDto>();
            var nowUtc = DateTime.UtcNow;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var createdTasks = new List<(UserTask Task, CreateTaskRequest Request)>();

                foreach (var req in requests)
                {
                    var startAt = NormalizeIncomingTaskTime(req.StartAt);
                    var endAt = NormalizeIncomingTaskTime(req.EndAt);
                    var dueAt = NormalizeIncomingTaskTime(req.DueAt) ?? endAt;

                    if (req.TaskType == "Schedule" && startAt.HasValue && endAt.HasValue && endAt <= startAt)
                    {
                        throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");
                    }

                    var task = new UserTask
                    {
                        UserId = userId,
                        Title = req.Title.Trim(),
                        Description = req.Description?.Trim(),
                        StartAt = startAt,
                        EndAt = endAt,
                        DueAt = dueAt,
                        Status = "Pending",
                        Priority = req.Priority,
                        RepeatType = req.RepeatType,
                        TaskType = req.TaskType,
                        Location = req.Location?.Trim(),
                        RawText = req.RawText?.Trim(),
                        CreatedAt = nowUtc
                    };

                    _context.UserTasks.Add(task);
                    createdTasks.Add((task, req));
                }

                await _context.SaveChangesAsync();

                var remindersToAdd = new List<Reminder>();
                foreach (var (task, req) in createdTasks)
                {
                    var reminderReferenceTime = GetReminderReferenceTime(task);
                    if (req.ReminderBeforeMinutes.HasValue && reminderReferenceTime.HasValue)
                    {
                        var remindTime = reminderReferenceTime.Value.AddMinutes(-req.ReminderBeforeMinutes.Value);
                        remindersToAdd.Add(new Reminder
                        {
                            UserId = userId,
                            TaskId = task.TaskId,
                            Title = task.Title,
                            Message = task.TaskType == "Schedule"
                                ? $"Sắp đến giờ diễn ra lịch trình: {task.Title}"
                                : $"Sắp đến giờ thực hiện công việc: {task.Title}",
                            RemindAt = remindTime,
                            IsSent = false,
                            CreatedAt = nowUtc
                        });
                    }

                    results.Add(new TaskDto
                    {
                        TaskId = task.TaskId,
                        UserId = task.UserId,
                        Title = task.Title,
                        Description = task.Description,
                        StartAt = task.StartAt,
                        EndAt = task.EndAt,
                        DueAt = task.DueAt,
                        Status = task.Status,
                        Priority = task.Priority,
                        RepeatType = task.RepeatType,
                        RawText = task.RawText,
                        CreatedAt = task.CreatedAt,
                        IsOverdue = task.TaskType == "Task" && task.DueAt.HasValue && task.DueAt.Value < nowUtc && task.Status == "Pending",
                        ReminderBeforeMinutes = req.ReminderBeforeMinutes,
                        TaskType = task.TaskType,
                        Location = task.Location
                    });
                }

                if (remindersToAdd.Any())
                {
                    _context.Reminders.AddRange(remindersToAdd);
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return results;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<TaskDto> UpdateTaskAsync(Guid userId, long taskId, UpdateTaskRequest request)
        {
            var startAt = NormalizeIncomingTaskTime(request.StartAt);
            var endAt = NormalizeIncomingTaskTime(request.EndAt);
            var dueAt = NormalizeIncomingTaskTime(request.DueAt) ?? endAt;

            if (request.TaskType == "Schedule" && startAt.HasValue && endAt.HasValue && endAt <= startAt)
            {
                throw new ArgumentException("Giờ kết thúc phải sau giờ bắt đầu.");
            }

            var task = await _context.UserTasks.FirstOrDefaultAsync(t => t.UserId == userId && t.TaskId == taskId);
            if (task == null)
            {
                throw new KeyNotFoundException("Không tìm thấy công việc hoặc bạn không có quyền sửa.");
            }

            task.Title = request.Title.Trim();
            task.Description = request.Description?.Trim();
            task.StartAt = startAt;
            task.EndAt = endAt;
            task.DueAt = dueAt;
            task.Status = request.Status;
            task.Priority = request.Priority;
            task.RepeatType = request.RepeatType;
            task.TaskType = request.TaskType;
            task.Location = request.Location?.Trim();
            task.UpdatedAt = DateTime.UtcNow;


            if (request.Status == "Done" && !task.CompletedAt.HasValue)
            {
                task.CompletedAt = DateTime.UtcNow;
            }
            else if (request.Status != "Done")
            {
                task.CompletedAt = null;
            }

            await _context.SaveChangesAsync();

            // Update Reminders
            var existingReminders = await _context.Reminders.Where(r => r.TaskId == task.TaskId).ToListAsync();
            if (existingReminders.Any())
            {
                _context.Reminders.RemoveRange(existingReminders);
            }

            var reminderReferenceTime = GetReminderReferenceTime(task);
            if (request.ReminderBeforeMinutes.HasValue && reminderReferenceTime.HasValue)
            {
                var remindTime = reminderReferenceTime.Value.AddMinutes(-request.ReminderBeforeMinutes.Value);
                var reminder = new Reminder
                {
                    UserId = userId,
                    TaskId = task.TaskId,
                    Title = task.Title,
                    Message = task.TaskType == "Schedule" 
                        ? $"Sắp đến giờ diễn ra lịch trình: {task.Title}" 
                        : $"Sắp đến giờ thực hiện công việc: {task.Title}",
                    RemindAt = remindTime,
                    IsSent = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Reminders.Add(reminder);
            }

            await _context.SaveChangesAsync();

            return new TaskDto
            {
                TaskId = task.TaskId,
                UserId = task.UserId,
                Title = task.Title,
                Description = task.Description,
                StartAt = task.StartAt,
                EndAt = task.EndAt,
                DueAt = task.DueAt,
                Status = task.Status,
                Priority = task.Priority,
                RepeatType = task.RepeatType,
                RawText = task.RawText,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                CompletedAt = task.CompletedAt,
                IsOverdue = task.TaskType == "Task" && task.DueAt.HasValue && task.DueAt.Value < DateTime.UtcNow && task.Status == "Pending",
                ReminderBeforeMinutes = request.ReminderBeforeMinutes,
                TaskType = task.TaskType,
                Location = task.Location
            };
        }

        public async Task<bool> DeleteTaskAsync(Guid userId, long taskId)
        {
            var task = await _context.UserTasks.FirstOrDefaultAsync(t => t.UserId == userId && t.TaskId == taskId);
            if (task == null)
            {
                return false;
            }

            _context.UserTasks.Remove(task);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<TaskDto> CompleteTaskAsync(Guid userId, long taskId)
        {
            var task = await _context.UserTasks.FirstOrDefaultAsync(t => t.UserId == userId && t.TaskId == taskId);
            if (task == null)
            {
                throw new KeyNotFoundException("Không tìm thấy công việc.");
            }


            task.Status = "Done";
            task.CompletedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return new TaskDto
            {
                TaskId = task.TaskId,
                UserId = task.UserId,
                Title = task.Title,
                Description = task.Description,
                StartAt = task.StartAt,
                EndAt = task.EndAt,
                DueAt = task.DueAt,
                Status = task.Status,
                Priority = task.Priority,
                RepeatType = task.RepeatType,
                RawText = task.RawText,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                CompletedAt = task.CompletedAt,
                IsOverdue = false
            };
        }

        private static DateTime? GetReminderReferenceTime(UserTask task)
        {
            return string.Equals(task.TaskType, "Schedule", StringComparison.OrdinalIgnoreCase)
                ? task.StartAt ?? task.DueAt
                : task.DueAt;
        }

        private static DateTime? NormalizeIncomingTaskTime(DateTime? value)
        {
            if (!value.HasValue) return null;

            var dateTime = value.Value;
            if (dateTime.Kind == DateTimeKind.Utc)
            {
                return dateTime;
            }

            return DateTimeHelper.ConvertToUtc(dateTime, "Asia/Ho_Chi_Minh");
        }

        private static int? CalculateReminderBeforeMinutes(UserTask task)
        {
            var reminder = task.Reminders.OrderBy(r => r.RemindAt).FirstOrDefault();
            if (reminder == null) return null;

            var referenceTime = GetReminderReferenceTime(task);
            if (!referenceTime.HasValue) return null;

            var minutes = (referenceTime.Value - reminder.RemindAt).TotalMinutes;

            // Older schedules stored reminders relative to EndAt/DueAt.
            if (string.Equals(task.TaskType, "Schedule", StringComparison.OrdinalIgnoreCase) &&
                minutes <= 0 &&
                task.DueAt.HasValue)
            {
                minutes = (task.DueAt.Value - reminder.RemindAt).TotalMinutes;
            }

            return minutes > 0 ? (int?)Math.Round(minutes) : null;
        }

        private static TaskDto ToTaskDto(UserTask task, DateTime now)
        {
            return new TaskDto
            {
                TaskId = task.TaskId,
                UserId = task.UserId,
                Title = task.Title,
                Description = task.Description,
                StartAt = task.StartAt,
                EndAt = task.EndAt,
                DueAt = task.DueAt,
                Status = task.Status,
                Priority = task.Priority,
                RepeatType = task.RepeatType,
                RawText = task.RawText,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt,
                CompletedAt = task.CompletedAt,
                IsOverdue = task.TaskType == "Task" && task.DueAt.HasValue && task.DueAt.Value < now && task.Status == "Pending",
                ReminderBeforeMinutes = CalculateReminderBeforeMinutes(task),
                TaskType = task.TaskType,
                Location = task.Location
            };
        }
    }
}
