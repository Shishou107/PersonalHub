using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiDailyExpense.Api.DTOs.Reminders;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface IReminderService
    {
        Task<List<ReminderDto>> GetRemindersAsync(Guid userId, bool upcomingOnly);
        Task<ReminderDto> CreateReminderAsync(Guid userId, CreateReminderRequest request);
        Task<bool> DeleteReminderAsync(Guid userId, long reminderId);
    }
}
