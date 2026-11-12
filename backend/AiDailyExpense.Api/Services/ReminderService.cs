using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Reminders;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class ReminderService : IReminderService
    {
        private readonly AppDbContext _context;

        public ReminderService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<ReminderDto>> GetRemindersAsync(Guid userId, bool upcomingOnly)
        {
            var query = _context.Reminders
                .Include(r => r.Task)
                .Where(r => r.UserId == userId);

            if (upcomingOnly)
            {
                query = query.Where(r => r.RemindAt >= DateTime.UtcNow && !r.IsSent);
            }

            var reminders = await query
                .OrderBy(r => r.RemindAt)
                .Select(r => new ReminderDto
                {
                    ReminderId = r.ReminderId,
                    UserId = r.UserId,
                    TaskId = r.TaskId,
                    TaskTitle = r.Task != null ? r.Task.Title : null,
                    Title = r.Title,
                    Message = r.Message,
                    RemindAt = r.RemindAt,
                    IsSent = r.IsSent,
                    SentAt = r.SentAt,
                    CreatedAt = r.CreatedAt
                })
                .ToListAsync();

            return reminders;
        }

        public async Task<ReminderDto> CreateReminderAsync(Guid userId, CreateReminderRequest request)
        {
            if (request.TaskId.HasValue)
            {
                var taskExists = await _context.UserTasks.AnyAsync(t => t.TaskId == request.TaskId.Value && t.UserId == userId);
                if (!taskExists)
                {
                    throw new ArgumentException("Mã công việc không tồn tại hoặc không thuộc quyền sở hữu.");
                }
            }

            var reminder = new Reminder
            {
                UserId = userId,
                TaskId = request.TaskId,
                Title = request.Title.Trim(),
                Message = request.Message?.Trim(),
                RemindAt = request.RemindAt,
                IsSent = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reminders.Add(reminder);
            await _context.SaveChangesAsync();

            string? taskTitle = null;
            if (reminder.TaskId.HasValue)
            {
                var task = await _context.UserTasks.FindAsync(reminder.TaskId.Value);
                taskTitle = task?.Title;
            }

            return new ReminderDto
            {
                ReminderId = reminder.ReminderId,
                UserId = reminder.UserId,
                TaskId = reminder.TaskId,
                TaskTitle = taskTitle,
                Title = reminder.Title,
                Message = reminder.Message,
                RemindAt = reminder.RemindAt,
                IsSent = reminder.IsSent,
                SentAt = reminder.SentAt,
                CreatedAt = reminder.CreatedAt
            };
        }

        public async Task<bool> DeleteReminderAsync(Guid userId, long reminderId)
        {
            var reminder = await _context.Reminders.FirstOrDefaultAsync(r => r.UserId == userId && r.ReminderId == reminderId);
            if (reminder == null)
            {
                return false;
            }

            _context.Reminders.Remove(reminder);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
