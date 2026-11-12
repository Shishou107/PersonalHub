using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiDailyExpense.Api.DTOs.Tasks;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface ITaskService
    {
        Task<List<TaskDto>> GetTasksAsync(Guid userId, DateTime? date, string? status);
        Task<List<TaskDto>> GetTodayTasksAsync(Guid userId);
        Task<TaskDto?> GetTaskByIdAsync(Guid userId, long taskId);
        Task<TaskDto> CreateTaskAsync(Guid userId, CreateTaskRequest request);
        Task<List<TaskDto>> CreateTasksBatchAsync(Guid userId, List<CreateTaskRequest> requests);
        Task<TaskDto> UpdateTaskAsync(Guid userId, long taskId, UpdateTaskRequest request);
        Task<bool> DeleteTaskAsync(Guid userId, long taskId);
        Task<TaskDto> CompleteTaskAsync(Guid userId, long taskId);
    }
}
