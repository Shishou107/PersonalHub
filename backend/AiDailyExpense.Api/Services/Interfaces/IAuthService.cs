using System;
using System.Threading.Tasks;
using AiDailyExpense.Api.DTOs.Auth;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResponse> RegisterAsync(RegisterRequest request);
        Task<AuthResponse> LoginAsync(LoginRequest request);
        Task<UserDto> GetUserByIdAsync(Guid userId);
        Task<UserDto> InitializeBalanceAsync(Guid userId, InitializeBalanceRequest request);
        Task RequestEmailVerificationAsync(string email);
        Task VerifyEmailAsync(string email, string code);
        Task RequestPasswordResetAsync(string email);
        Task ResetPasswordAsync(ResetPasswordRequest request);
    }
}
