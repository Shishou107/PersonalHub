using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Devices;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _context;

        public NotificationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<bool> RegisterDeviceTokenAsync(Guid userId, RegisterDeviceTokenRequest request)
        {
            var existing = await _context.DeviceTokens
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Token == request.Token);

            if (existing != null)
            {
                existing.IsActive = true;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                var dt = new DeviceToken
                {
                    UserId = userId,
                    Token = request.Token,
                    Platform = request.Platform,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _context.DeviceTokens.Add(dt);
            }

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> SendNotificationAsync(Guid userId, string title, string message)
        {
            // In a real application, fetch the active device tokens for the user and push using Firebase or Expo Notifications
            var tokens = await _context.DeviceTokens
                .Where(t => t.UserId == userId && t.IsActive)
                .Select(t => t.Token)
                .ToListAsync();

            Console.WriteLine($"[Mock Notification] Gửi tới User: {userId}");
            Console.WriteLine($"Tiêu đề: {title}");
            Console.WriteLine($"Nội dung: {message}");
            Console.WriteLine($"Số lượng thiết bị nhận: {tokens.Count}");

            return true;
        }
    }
}
