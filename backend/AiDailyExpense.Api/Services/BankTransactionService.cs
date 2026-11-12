using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.BankTransactions;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class BankTransactionService : IBankTransactionService
    {
        private readonly AppDbContext _context;
        private readonly IExpenseService _expenseService;
        private readonly IAiService _aiService;

        public BankTransactionService(AppDbContext context, IExpenseService expenseService, IAiService aiService)
        {
            _context = context;
            _expenseService = expenseService;
            _aiService = aiService;
        }

        public async Task<List<BankTransactionDto>> GetBankTransactionsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.BankTransactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId);

            if (fromDate.HasValue || toDate.HasValue)
            {
                var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                var timeZoneId = user?.TimeZone ?? "Asia/Ho_Chi_Minh";
                var tz = Helpers.DateTimeHelper.GetUserTimeZone(timeZoneId);

                if (fromDate.HasValue)
                {
                    var fromLocal = fromDate.Value.Date;
                    var fromUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(fromLocal, DateTimeKind.Unspecified), tz);
                    query = query.Where(t => (t.TransactionTime ?? t.CreatedAt) >= fromUtc);
                }

                if (toDate.HasValue)
                {
                    var toLocalNextDay = toDate.Value.Date.AddDays(1);
                    var toUtcNextDay = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(toLocalNextDay, DateTimeKind.Unspecified), tz);
                    query = query.Where(t => (t.TransactionTime ?? t.CreatedAt) < toUtcNextDay);
                }
            }

            return await query
                .OrderByDescending(t => t.TransactionTime ?? t.CreatedAt)
                .Select(t => new BankTransactionDto
                {
                    BankTransactionId = t.BankTransactionId,
                    UserId = t.UserId,
                    BankName = t.BankName,
                    AccountLabel = t.AccountLabel,
                    TransactionType = t.TransactionType,
                    Amount = t.Amount,
                    Currency = t.Currency,
                    Description = t.Description,
                    CategoryId = t.CategoryId,
                    CategoryName = t.Category != null ? t.Category.Name : "Khác",
                    TransactionTime = t.TransactionTime,
                    Source = t.Source,
                    RawMessage = t.RawMessage,
                    IsConfirmed = t.IsConfirmed,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<BankTransactionDto> ProcessNotificationAsync(Guid userId, BankNotificationRequest request)
        {
            var rawText = request.RawMessage.Trim();
            var normalizedText = NormalizeNotificationForHash(rawText);
            var hash = ComputeSha256Hash(userId.ToString() + "_" + normalizedText);
            var legacyHash = ComputeSha256Hash(userId.ToString() + "_" + rawText);
            var receivedAt = request.ReceivedAt ?? DateTime.UtcNow;

            var existingBeforeAi = await FindExistingNotificationAsync(userId, rawText, hash, legacyHash, receivedAt);
            if (existingBeforeAi != null)
            {
                if (string.IsNullOrWhiteSpace(existingBeforeAi.RawMessageHash) || existingBeforeAi.RawMessageHash == legacyHash)
                {
                    existingBeforeAi.RawMessageHash = hash;
                    await _context.SaveChangesAsync();
                }

                return ToDto(existingBeforeAi);
            }

            var parsed = await AnalyzeNotificationAsync(userId, request.BankName, rawText);

            if (parsed.Amount <= 0)
            {
                throw new ArgumentException("Không thể phân tích số tiền hợp lệ từ thông báo.");
            }

            var existing = await FindExistingNotificationAsync(userId, rawText, hash, legacyHash, receivedAt);
            if (existing != null)
            {
                existing.Amount = parsed.Amount;
                existing.TransactionType = parsed.TransactionType;
                existing.Description = parsed.Description;
                existing.CategoryId = parsed.CategoryId;
                existing.RawMessageHash = hash;
                await _context.SaveChangesAsync();

                return ToDto(existing);
            }

            var tx = new BankTransaction
            {
                UserId = userId,
                BankName = request.BankName ?? "Ngân hàng",
                AccountLabel = ExtractAccountLabel(rawText) ?? "Tài khoản giả lập",
                TransactionType = parsed.TransactionType,
                Amount = parsed.Amount,
                Currency = "VND",
                Description = parsed.Description,
                CategoryId = parsed.CategoryId,
                TransactionTime = receivedAt,
                Source = "Notification",
                RawMessage = rawText,
                RawMessageHash = hash,
                IsConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.BankTransactions.Add(tx);
            await _context.SaveChangesAsync();

            try
            {
                var createReq = new CreateExpenseRequest
                {
                    Title = tx.Description ?? "Chi tiêu từ ngân hàng",
                    Amount = tx.Amount,
                    CategoryId = tx.CategoryId,
                    SpentAt = tx.TransactionTime ?? DateTime.UtcNow,
                    PaymentMethod = "Transfer",
                    Note = $"Tự động ghi nhận từ thông báo ({tx.BankName})",
                    RawText = tx.RawMessage
                };
                await _expenseService.CreateExpenseAsync(userId, createReq);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AutoConfirm] Error creating expense: {ex.Message}");
            }

            if (tx.CategoryId.HasValue)
            {
                tx.Category = await _context.ExpenseCategories.FindAsync(tx.CategoryId.Value);
            }

            return ToDto(tx);
        }

        private async Task<BankTransaction?> FindExistingNotificationAsync(
            Guid userId,
            string rawText,
            string hash,
            string legacyHash,
            DateTime receivedAt)
        {
            var lowerBound = receivedAt.AddHours(-48);
            var upperBound = receivedAt.AddHours(48);

            return await _context.BankTransactions
                .Include(t => t.Category)
                .Where(t => t.UserId == userId &&
                    (t.RawMessageHash == hash || t.RawMessageHash == legacyHash || t.RawMessage == rawText) &&
                    ((t.TransactionTime ?? t.CreatedAt) >= lowerBound && (t.TransactionTime ?? t.CreatedAt) <= upperBound))
                .OrderByDescending(t => t.TransactionTime ?? t.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task<ExpenseDto> ConfirmTransactionAsync(Guid userId, long bankTransactionId, ConfirmBankTransactionRequest request)
        {
            var tx = await _context.BankTransactions
                .FirstOrDefaultAsync(t => t.UserId == userId && t.BankTransactionId == bankTransactionId);

            if (tx == null)
            {
                throw new KeyNotFoundException("Không tìm thấy giao dịch ngân hàng.");
            }

            if (tx.IsConfirmed)
            {
                throw new InvalidOperationException("Giao dịch này đã được xác nhận trước đó.");
            }

            int? categoryId = request.CategoryId ?? tx.CategoryId;

            var createReq = new CreateExpenseRequest
            {
                Title = tx.Description ?? "Chi tiêu từ ngân hàng",
                Amount = tx.Amount,
                CategoryId = categoryId,
                SpentAt = tx.TransactionTime ?? DateTime.UtcNow,
                PaymentMethod = "Transfer",
                Note = $"Xác nhận từ thông báo ({tx.BankName})",
                RawText = tx.RawMessage
            };

            var expense = await _expenseService.CreateExpenseAsync(userId, createReq);

            tx.IsConfirmed = true;
            tx.CategoryId = categoryId;
            await _context.SaveChangesAsync();

            return expense;
        }

        public async Task<bool> DeleteTransactionAsync(Guid userId, long bankTransactionId)
        {
            var tx = await _context.BankTransactions
                .FirstOrDefaultAsync(t => t.UserId == userId && t.BankTransactionId == bankTransactionId);

            if (tx == null) return false;

            // Delete corresponding expense if it exists
            if (!string.IsNullOrWhiteSpace(tx.RawMessage))
            {
                var expense = await _context.Expenses
                    .FirstOrDefaultAsync(e => e.UserId == userId && e.RawText == tx.RawMessage);
                if (expense != null)
                {
                    await _expenseService.DeleteExpenseAsync(userId, expense.ExpenseId);
                }
            }

            _context.BankTransactions.Remove(tx);
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<ParsedBankNotification> AnalyzeNotificationAsync(Guid userId, string? sourceApp, string rawText)
        {
            var aiResult = await _aiService.AnalyzeFinancialNotificationAsync(
                userId,
                sourceApp,
                null,
                rawText);

            if (!aiResult.IsTransaction)
            {
                throw new ArgumentException(string.IsNullOrWhiteSpace(aiResult.IgnoreReason)
                    ? "Thông báo này không phải giao dịch tài chính."
                    : aiResult.IgnoreReason);
            }

            var amount = aiResult.Amount > 0 ? aiResult.Amount : MoneyParser.Parse(rawText);
            var type = aiResult.TransactionType == "Unknown" ? DetectTransactionType(rawText) : aiResult.TransactionType;
            var categoryId = await SuggestExpenseCategoryIdAsync(userId, rawText, aiResult.CategoryName);
            var description = !string.IsNullOrWhiteSpace(aiResult.Description)
                ? aiResult.Description
                : ExtractDescription(rawText, aiResult.Title);

            return new ParsedBankNotification
            {
                Amount = amount,
                TransactionType = type,
                CategoryId = categoryId,
                Description = description
            };
        }

        private string DetectTransactionType(string rawText)
        {
            var text = rawText.ToLowerInvariant();

            if (text.Contains("+") || text.Contains("nhan") || text.Contains("nhận") ||
                text.Contains("luong") || text.Contains("lương") || text.Contains("cong") ||
                text.Contains("cộng") || text.Contains("credit"))
            {
                return "Income";
            }

            if (text.Contains("-") || text.Contains("thanh toan") || text.Contains("thanh toán") ||
                text.Contains("rut") || text.Contains("rút") || text.Contains("chi tieu") ||
                text.Contains("chi tiêu") || text.Contains("mua") || text.Contains("payment") ||
                text.Contains("chuyen") || text.Contains("chuyển"))
            {
                return "Expense";
            }

            return "Unknown";
        }

        private async Task<int?> SuggestExpenseCategoryIdAsync(Guid userId, string rawText, string? aiCategoryName)
        {
            var aiCategoryId = await FindCategoryIdAsync(userId, aiCategoryName);
            if (aiCategoryId.HasValue) return aiCategoryId;

            var text = rawText.ToLowerInvariant();
            if (text.Contains("circle k") || text.Contains("cafe") || text.Contains("cà phê") ||
                text.Contains("phở") || text.Contains("cơm") || text.Contains("ăn") ||
                text.Contains("highlands") || text.Contains("coffee"))
            {
                return await FindCategoryIdAsync(userId, "Ăn uống", "Expense");
            }

            if (text.Contains("xăng") || text.Contains("grab") || text.Contains("taxi") ||
                text.Contains("xe") || text.Contains("be "))
            {
                return await FindCategoryIdAsync(userId, "Di chuyển", "Expense");
            }

            if (text.Contains("shopee") || text.Contains("lazada") || text.Contains("tiki") ||
                text.Contains("mua sắm"))
            {
                return await FindCategoryIdAsync(userId, "Mua sắm", "Expense");
            }

            return null;
        }

        private async Task<int?> FindCategoryIdAsync(Guid userId, string? categoryName, string? type = null)
        {
            if (string.IsNullOrWhiteSpace(categoryName)) return null;

            var normalizedName = categoryName.Trim().ToLower();
            var query = _context.ExpenseCategories
                .Where(c => (c.UserId == null || c.UserId == userId) && c.Name.ToLower() == normalizedName);

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(c => c.Type == type);
            }

            var category = await query.FirstOrDefaultAsync();
            return category?.CategoryId;
        }

        private string ExtractDescription(string rawText, string? aiTitle)
        {
            var merchantPatterns = new[]
            {
                @"(?:tai|tại)\s+(?<name>.+?)\s+(?:luc|lúc|ngay|ngày|vao|vào|so du|số dư|$)",
                @"(?:den|đến|toi|tới)\s+(?<name>.+?)\s+(?:luc|lúc|ngay|ngày|vao|vào|$)",
                @"(?:thanh toan|thanh toán)\s+(?<name>.+?)\s+(?:luc|lúc|ngay|ngày|vao|vào|$)"
            };

            foreach (var pattern in merchantPatterns)
            {
                var match = Regex.Match(rawText, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var name = CleanDescription(match.Groups["name"].Value);
                    if (!string.IsNullOrWhiteSpace(name)) return name;
                }
            }

            var cleanedAiTitle = CleanDescription(aiTitle);
            return string.IsNullOrWhiteSpace(cleanedAiTitle) ? "Giao dịch ngân hàng" : cleanedAiTitle;
        }

        private string CleanDescription(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var cleaned = Regex.Replace(value, @"[+-]?\s*\d{1,3}(?:[.,]\d{3})*(?:\s*(?:vnd|vnđ|đ|d|k|tr|triệu))?", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\b(tk|stk|x\d+|vnd|vnđ|luc|lúc|ngay|ngày)\b", "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim(' ', '.', '-', ':');

            return cleaned;
        }

        private string? ExtractAccountLabel(string rawText)
        {
            var match = Regex.Match(rawText, @"\b(?:tk|stk)\s*([A-Za-z]?\d{3,})\b", RegexOptions.IgnoreCase);
            return match.Success ? $"TK {match.Groups[1].Value}" : null;
        }

        private BankTransactionDto ToDto(BankTransaction tx)
        {
            return new BankTransactionDto
            {
                BankTransactionId = tx.BankTransactionId,
                UserId = tx.UserId,
                BankName = tx.BankName,
                AccountLabel = tx.AccountLabel,
                TransactionType = tx.TransactionType,
                Amount = tx.Amount,
                Currency = tx.Currency,
                Description = tx.Description,
                CategoryId = tx.CategoryId,
                CategoryName = tx.Category != null ? tx.Category.Name : "Khác",
                TransactionTime = tx.TransactionTime,
                Source = tx.Source,
                RawMessage = tx.RawMessage,
                IsConfirmed = tx.IsConfirmed,
                CreatedAt = tx.CreatedAt
            };
        }

        private string ComputeSha256Hash(string rawData)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        private string NormalizeNotificationForHash(string rawText)
        {
            var normalized = rawText.Trim().ToLowerInvariant();
            normalized = Regex.Replace(normalized, @"\s+", " ");
            return normalized;
        }

        private class ParsedBankNotification
        {
            public decimal Amount { get; set; }
            public string TransactionType { get; set; } = "Unknown";
            public int? CategoryId { get; set; }
            public string Description { get; set; } = "Giao dịch ngân hàng";
        }
    }
}
