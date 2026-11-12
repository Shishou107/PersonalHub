using System;
using System.Threading.Tasks;
using AiDailyExpense.Api.DTOs.Devices;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface INotificationService
    {
        Task<bool> RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request);
        Task<bool> SendNotificationAsync(Guid userId, string title, string message);
    }
}
