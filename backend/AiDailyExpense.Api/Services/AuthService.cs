using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Auth;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly JwtHelper _jwtHelper;
        private readonly IEmailService _emailService;

        public AuthService(AppDbContext context, JwtHelper jwtHelper, IEmailService emailService)
        {
            _context = context;
            _jwtHelper = jwtHelper;
            _emailService = emailService;
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            var emailLower = request.Email.Trim().ToLower();
            var existingUser = await _context.Users.AnyAsync(u => u.Email.ToLower() == emailLower);
            if (existingUser)
            {
                throw new ArgumentException("Email này đã được sử dụng.");
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            var user = new User
            {
                UserId = Guid.NewGuid(),
                FullName = request.FullName.Trim(),
                Email = emailLower,
                PasswordHash = passwordHash,
                IsEmailVerified = false,
                TimeZone = "Asia/Ho_Chi_Minh",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            try
            {
                await CreateAndSendCodeAsync(user.Email, "EmailVerification", user.UserId, "Mã xác thực tài khoản");
            }
            catch
            {
                // Registration should not fail in local development when SMTP is not configured.
            }

            // Generate token
            var token = _jwtHelper.GenerateToken(user);

            return new AuthResponse
            {
                Token = token,
                User = ToUserDto(user)
            };
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var emailLower = request.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);
            if (user == null || !user.IsActive)
            {
                throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác.");
            }

            if (!user.IsEmailVerified)
            {
                throw new UnauthorizedAccessException("Tài khoản chưa được xác thực email. Vui lòng xác thực email trước khi đăng nhập.");
            }

            bool isPasswordValid = false;
            bool shouldUpgradeHash = false;

            if (user.PasswordHash.StartsWith("$2"))
            {
                try
                {
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                }
                catch
                {
                    isPasswordValid = (request.Password == user.PasswordHash);
                    shouldUpgradeHash = isPasswordValid;
                }
            }
            else
            {
                isPasswordValid = (request.Password == user.PasswordHash);
                shouldUpgradeHash = isPasswordValid;
            }

            if (!isPasswordValid)
            {
                throw new UnauthorizedAccessException("Email hoặc mật khẩu không chính xác.");
            }

            if (shouldUpgradeHash)
            {
                try
                {
                    user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
                    await _context.SaveChangesAsync();
                }
                catch
                {
                    // Ignore upgrade failure to allow session connection
                }
            }

            var token = _jwtHelper.GenerateToken(user);

            return new AuthResponse
            {
                Token = token,
                User = ToUserDto(user)
            };
        }

        public async Task<UserDto> GetUserByIdAsync(Guid userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy người dùng.");
            }

            return ToUserDto(user);
        }

        public async Task<UserDto> InitializeBalanceAsync(Guid userId, InitializeBalanceRequest request)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy người dùng.");
            }
            if (user.HasInitializedBalance)
            {
                throw new InvalidOperationException("Số dư ban đầu đã được thiết lập.");
            }

            user.CashBalance = request.CashBalance;
            user.BankBalance = request.BankBalance;
            user.HasInitializedBalance = true;
            await _context.SaveChangesAsync();

            return ToUserDto(user);
        }

        public async Task RequestEmailVerificationAsync(string email)
        {
            var emailLower = email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài khoản.");
            }

            await CreateAndSendCodeAsync(user.Email, "EmailVerification", user.UserId, "Mã xác thực tài khoản");
        }

        public async Task VerifyEmailAsync(string email, string code)
        {
            var emailLower = email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài khoản.");
            }

            var token = await ConsumeCodeAsync(emailLower, "EmailVerification", code);
            user.IsEmailVerified = true;
            user.EmailVerifiedAt = DateTime.UtcNow;
            token.UserId = user.UserId;
            await _context.SaveChangesAsync();
        }

        public async Task RequestPasswordResetAsync(string email)
        {
            var emailLower = email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);
            if (user == null)
            {
                return;
            }

            await CreateAndSendCodeAsync(user.Email, "PasswordReset", user.UserId, "Mã đặt lại mật khẩu");
        }

        public async Task ResetPasswordAsync(ResetPasswordRequest request)
        {
            var emailLower = request.Email.Trim().ToLower();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == emailLower);
            if (user == null)
            {
                throw new KeyNotFoundException("Không tìm thấy tài khoản.");
            }

            await ConsumeCodeAsync(emailLower, "PasswordReset", request.Code);
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();
        }

        private async Task CreateAndSendCodeAsync(string email, string purpose, Guid? userId, string subject)
        {
            var emailLower = email.Trim().ToLower();
            var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");

            var activeCodes = await _context.EmailVerificationCodes
                .Where(c => c.Email == emailLower && c.Purpose == purpose && c.ConsumedAt == null)
                .ToListAsync();
            foreach (var activeCode in activeCodes)
            {
                activeCode.ConsumedAt = DateTime.UtcNow;
            }

            _context.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                Email = emailLower,
                Purpose = purpose,
                CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await _emailService.SendCodeAsync(emailLower, subject, code);
        }

        private async Task<EmailVerificationCode> ConsumeCodeAsync(string email, string purpose, string code)
        {
            var now = DateTime.UtcNow;
            var candidates = await _context.EmailVerificationCodes
                .Where(c => c.Email == email && c.Purpose == purpose && c.ConsumedAt == null && c.ExpiresAt > now)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .ToListAsync();

            var match = candidates.FirstOrDefault(c => BCrypt.Net.BCrypt.Verify(code, c.CodeHash));
            if (match == null)
            {
                throw new UnauthorizedAccessException("Mã xác thực không đúng hoặc đã hết hạn.");
            }

            match.ConsumedAt = now;
            return match;
        }

        private static UserDto ToUserDto(User user)
        {
            return new UserDto
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                CashBalance = user.CashBalance,
                BankBalance = user.BankBalance,
                TotalBalance = user.CashBalance + user.BankBalance,
                HasInitializedBalance = user.HasInitializedBalance,
                HasVaultPin = !string.IsNullOrWhiteSpace(user.VaultPinHash)
            };
        }
    }
}
