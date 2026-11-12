using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Vault;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class VaultService : IVaultService
    {
        private const string VaultAccessPurpose = "VaultAccess";
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;

        public VaultService(AppDbContext context, IEmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
        }

        public async Task<List<VaultCategoryDto>> GetCategoriesAsync(Guid userId)
        {
            return await _context.VaultCategories
                .Where(c => c.UserId == null || c.UserId == userId)
                .OrderByDescending(c => c.IsDefault)
                .ThenBy(c => c.Name)
                .Select(c => new VaultCategoryDto
                {
                    VaultCategoryId = c.VaultCategoryId,
                    Name = c.Name,
                    Icon = c.Icon,
                    Color = c.Color
                })
                .ToListAsync();
        }

        public async Task<List<VaultItemDto>> GetItemsAsync(Guid userId, string? keyword)
        {
            var query = _context.VaultItems
                .Include(i => i.VaultCategory)
                .Where(i => i.UserId == userId && !i.IsArchived);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = keyword.Trim().ToLower();
                query = query.Where(i =>
                    i.Title.ToLower().Contains(kw) ||
                    (i.WebsiteUrl != null && i.WebsiteUrl.ToLower().Contains(kw)) ||
                    (i.LoginUsername != null && i.LoginUsername.ToLower().Contains(kw)) ||
                    (i.LoginEmail != null && i.LoginEmail.ToLower().Contains(kw)));
            }

            return await query
                .OrderByDescending(i => i.Favorite)
                .ThenByDescending(i => i.UpdatedAt ?? i.CreatedAt)
                .Select(i => ToDto(i))
                .ToListAsync();
        }

        public async Task<VaultItemDto> CreateItemAsync(Guid userId, CreateVaultItemRequest request)
        {
            var encryptedPassword = Encrypt(request.Password);
            (byte[] Cipher, byte[] Nonce, byte[] Tag)? encryptedNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : Encrypt(request.Notes);

            var item = new VaultItem
            {
                UserId = userId,
                VaultCategoryId = request.VaultCategoryId,
                Title = request.Title.Trim(),
                WebsiteUrl = request.WebsiteUrl?.Trim(),
                LoginUsername = request.LoginUsername?.Trim(),
                LoginEmail = request.LoginEmail?.Trim(),
                EncryptedPassword = encryptedPassword.Cipher,
                PasswordNonce = encryptedPassword.Nonce,
                PasswordAuthTag = encryptedPassword.Tag,
                EncryptedNotes = encryptedNotes?.Cipher,
                NotesNonce = encryptedNotes?.Nonce,
                NotesAuthTag = encryptedNotes?.Tag,
                Favorite = request.Favorite,
                PasswordChangedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.VaultItems.Add(item);
            await _context.SaveChangesAsync();
            await _context.Entry(item).Reference(i => i.VaultCategory).LoadAsync();
            return ToDto(item);
        }

        public async Task<VaultItemDto> UpdateItemAsync(Guid userId, long id, UpdateVaultItemRequest request)
        {
            var item = await _context.VaultItems
                .Include(i => i.VaultCategory)
                .FirstOrDefaultAsync(i => i.UserId == userId && i.VaultItemId == id);
            if (item == null) throw new KeyNotFoundException("Không tìm thấy tài khoản đã lưu.");

            var encryptedPassword = Encrypt(request.Password);
            (byte[] Cipher, byte[] Nonce, byte[] Tag)? encryptedNotes = string.IsNullOrWhiteSpace(request.Notes) ? null : Encrypt(request.Notes);

            item.VaultCategoryId = request.VaultCategoryId;
            item.Title = request.Title.Trim();
            item.WebsiteUrl = request.WebsiteUrl?.Trim();
            item.LoginUsername = request.LoginUsername?.Trim();
            item.LoginEmail = request.LoginEmail?.Trim();
            item.EncryptedPassword = encryptedPassword.Cipher;
            item.PasswordNonce = encryptedPassword.Nonce;
            item.PasswordAuthTag = encryptedPassword.Tag;
            item.EncryptedNotes = encryptedNotes?.Cipher;
            item.NotesNonce = encryptedNotes?.Nonce;
            item.NotesAuthTag = encryptedNotes?.Tag;
            item.Favorite = request.Favorite;
            item.PasswordChangedAt = DateTime.UtcNow;
            item.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            await _context.Entry(item).Reference(i => i.VaultCategory).LoadAsync();
            return ToDto(item);
        }

        public async Task<bool> DeleteItemAsync(Guid userId, long id)
        {
            var item = await _context.VaultItems.FirstOrDefaultAsync(i => i.UserId == userId && i.VaultItemId == id);
            if (item == null) return false;
            _context.VaultItems.Remove(item);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task SetPinAsync(Guid userId, SetVaultPinRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new KeyNotFoundException("Không tìm thấy người dùng.");

            // Verify old PIN if user has one
            if (!string.IsNullOrWhiteSpace(user.VaultPinHash))
            {
                if (string.IsNullOrWhiteSpace(request.OldPin))
                {
                    throw new ArgumentException("Vui lòng nhập PIN cũ để đổi PIN.");
                }
                if (!BCrypt.Net.BCrypt.Verify(request.OldPin, user.VaultPinHash))
                {
                    throw new UnauthorizedAccessException("PIN cũ không chính xác.");
                }
            }

            // Verify format (must be 6 digits)
            if (string.IsNullOrWhiteSpace(request.Pin) || request.Pin.Length != 6 || !request.Pin.All(char.IsDigit))
            {
                throw new ArgumentException("PIN mới phải gồm đúng 6 chữ số.");
            }

            user.VaultPinHash = BCrypt.Net.BCrypt.HashPassword(request.Pin);
            await _context.SaveChangesAsync();
        }

        public async Task RequestRevealCodeAsync(Guid userId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new KeyNotFoundException("Không tìm thấy người dùng.");

            var code = RandomNumberGenerator.GetInt32(0, 1000000).ToString("D6");
            var activeCodes = await _context.EmailVerificationCodes
                .Where(c => c.UserId == userId && c.Purpose == VaultAccessPurpose && c.ConsumedAt == null)
                .ToListAsync();
            foreach (var activeCode in activeCodes)
            {
                activeCode.ConsumedAt = DateTime.UtcNow;
            }

            _context.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                UserId = userId,
                Email = user.Email.ToLower(),
                Purpose = VaultAccessPurpose,
                CodeHash = BCrypt.Net.BCrypt.HashPassword(code),
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            await _emailService.SendCodeAsync(user.Email, "Mã mở kho tài khoản", code);
        }

        public async Task<RevealedVaultItemDto> RevealItemAsync(Guid userId, long id, RevealVaultItemRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null) throw new KeyNotFoundException("Không tìm thấy người dùng.");
            if (string.IsNullOrWhiteSpace(user.VaultPinHash))
            {
                throw new InvalidOperationException("Bạn chưa đặt PIN 6 số cho kho tài khoản.");
            }
            if (!BCrypt.Net.BCrypt.Verify(request.Pin, user.VaultPinHash))
            {
                throw new UnauthorizedAccessException("PIN không đúng.");
            }

            await ConsumeVaultCodeAsync(userId, request.EmailCode);

            var item = await _context.VaultItems.FirstOrDefaultAsync(i => i.UserId == userId && i.VaultItemId == id);
            if (item == null) throw new KeyNotFoundException("Không tìm thấy tài khoản đã lưu.");

            item.LastUsedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new RevealedVaultItemDto
            {
                VaultItemId = item.VaultItemId,
                Password = Decrypt(item.EncryptedPassword, item.PasswordNonce, item.PasswordAuthTag),
                Notes = item.EncryptedNotes == null || item.NotesNonce == null
                    ? null
                    : Decrypt(item.EncryptedNotes, item.NotesNonce, item.NotesAuthTag)
            };
        }

        private async Task ConsumeVaultCodeAsync(Guid userId, string code)
        {
            var now = DateTime.UtcNow;
            var candidates = await _context.EmailVerificationCodes
                .Where(c => c.UserId == userId && c.Purpose == VaultAccessPurpose && c.ConsumedAt == null && c.ExpiresAt > now)
                .OrderByDescending(c => c.CreatedAt)
                .Take(5)
                .ToListAsync();

            var match = candidates.FirstOrDefault(c => BCrypt.Net.BCrypt.Verify(code, c.CodeHash));
            if (match == null) throw new UnauthorizedAccessException("Mã email không đúng hoặc đã hết hạn.");

            match.ConsumedAt = now;
            await _context.SaveChangesAsync();
        }

        private (byte[] Cipher, byte[] Nonce, byte[] Tag) Encrypt(string plainText)
        {
            var nonce = RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var cipher = new byte[plainBytes.Length];

            using var aes = new AesGcm(GetMasterKey(), 16);
            aes.Encrypt(nonce, plainBytes, cipher, tag);
            return (cipher, nonce, tag);
        }

        private string Decrypt(byte[] cipher, byte[] nonce, byte[]? tag)
        {
            if (tag == null || tag.Length == 0) return string.Empty;
            var plainBytes = new byte[cipher.Length];
            using var aes = new AesGcm(GetMasterKey(), 16);
            aes.Decrypt(nonce, cipher, tag, plainBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }

        private byte[] GetMasterKey()
        {
            var secret = _configuration["Vault:MasterKey"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                secret = _configuration["Jwt:Key"];
            }
            if (string.IsNullOrWhiteSpace(secret))
            {
                secret = "AiDailyExpense_DefaultSecureVaultEncryptionMasterKey_2026!";
            }
            return SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        }

        private static VaultItemDto ToDto(VaultItem item)
        {
            return new VaultItemDto
            {
                VaultItemId = item.VaultItemId,
                VaultCategoryId = item.VaultCategoryId,
                CategoryName = item.VaultCategory?.Name,
                Title = item.Title,
                WebsiteUrl = item.WebsiteUrl,
                LoginUsername = item.LoginUsername,
                LoginEmail = item.LoginEmail,
                Favorite = item.Favorite,
                CreatedAt = item.CreatedAt,
                UpdatedAt = item.UpdatedAt
            };
        }
    }
}
