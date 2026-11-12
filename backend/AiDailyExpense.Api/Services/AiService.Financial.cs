using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Ai;
using AiDailyExpense.Api.DTOs.Expenses;
using AiDailyExpense.Api.DTOs.Tasks;
using AiDailyExpense.Api.Helpers;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public partial class AiService : IAiService
    {
        public Task<List<ParsedExpenseItemDto>> AnalyzeExpenseTextAsync(Guid userId, string text)
                {
                    var parsedItems = ParseExpenseItems(text.Trim())
                        .Select(item => new ParsedExpenseItemDto
                        {
                            ExpenseId = 0,
                            Title = item.Title,
                            Amount = item.Amount,
                            CategoryName = item.CategoryName,
                            CategoryType = item.CategoryType,
                            PaymentMethod = item.PaymentMethod
                        })
                        .ToList();
        
                    return Task.FromResult(parsedItems);
                }

        private async Task<List<ParsedExpenseInfo>> AnalyzeDailyExpenseItemsAsync(Guid userId, string text)
                {
                    var provider = _configuration["Ai:Provider"] ?? "Mock";
                    var geminiApiKey = _configuration["Ai:GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
                    var openAiApiKey = _configuration["Ai:OpenAiApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        
                    try
                    {
                        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(geminiApiKey))
                        {
                            return await AnalyzeDailyExpenseItemsWithGeminiAsync(userId, text, geminiApiKey);
                        }
        
                        if ((provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || provider.Equals("Orimise", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(openAiApiKey))
                        {
                            return await AnalyzeDailyExpenseItemsWithOpenAiAsync(userId, text, openAiApiKey);
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException("Đã hết token, vui lòng liên hệ admin!", ex);
                    }
        
                    throw new InvalidOperationException("Đã hết token, vui lòng liên hệ admin!");
                }

        private async Task<List<ParsedExpenseInfo>> AnalyzeDailyExpenseItemsWithGeminiAsync(Guid userId, string text, string apiKey)
                {
                    var categories = await _context.ExpenseCategories
                        .Where(c => (c.UserId == null || c.UserId == userId) && c.Type == "Expense")
                        .OrderBy(c => c.Name)
                        .Select(c => c.Name)
                        .ToListAsync();
        
                    var categoryLines = string.Join("\n", categories.Select(name => $"- {name}"));
                    var prompt = BuildDailyExpensePrompt(text, categoryLines);
                    var content = await GenerateJsonWithGeminiAsync(prompt, apiKey);
        
                    var parsed = JsonSerializer.Deserialize<AiDailyExpenseParseResult>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
                    if (parsed?.Items == null)
                    {
                        return new List<ParsedExpenseInfo>();
                    }
        
                    return parsed.Items
                        .Where(item => item.Amount > 0)
                        .Select(item => new ParsedExpenseInfo
                        {
                            Title = string.IsNullOrWhiteSpace(item.Title) ? "Chi tiêu khác" : item.Title.Trim(),
                            Amount = item.Amount,
                            CategoryName = string.IsNullOrWhiteSpace(item.CategoryName) ? "Khác" : item.CategoryName.Trim(),
                            PaymentMethod = NormalizeAiPaymentMethod(
                                string.IsNullOrWhiteSpace(item.PaymentMethod)
                                    ? DetectPaymentMethod(item.Title, text)
                                    : item.PaymentMethod)
                        })
                        .ToList();
                }

        private async Task<List<ParsedExpenseInfo>> AnalyzeDailyExpenseItemsWithOpenAiAsync(Guid userId, string text, string apiKey)
                {
                    var categories = await _context.ExpenseCategories
                        .Where(c => (c.UserId == null || c.UserId == userId) && c.Type == "Expense")
                        .OrderBy(c => c.Name)
                        .Select(c => c.Name)
                        .ToListAsync();
        
                    var categoryLines = string.Join("\n", categories.Select(name => $"- {name}"));
                    var prompt = BuildDailyExpensePrompt(text, categoryLines);
                    var content = await GenerateJsonWithOpenAiAsync(prompt, apiKey);
        
                    var parsed = JsonSerializer.Deserialize<AiDailyExpenseParseResult>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        
                    if (parsed?.Items == null)
                    {
                        return new List<ParsedExpenseInfo>();
                    }
        
                    return parsed.Items
                        .Where(item => item.Amount > 0)
                        .Select(item => new ParsedExpenseInfo
                        {
                            Title = string.IsNullOrWhiteSpace(item.Title) ? "Chi tiêu khác" : item.Title.Trim(),
                            Amount = item.Amount,
                            CategoryName = string.IsNullOrWhiteSpace(item.CategoryName) ? "Khác" : item.CategoryName.Trim(),
                            PaymentMethod = NormalizeAiPaymentMethod(
                                string.IsNullOrWhiteSpace(item.PaymentMethod)
                                    ? DetectPaymentMethod(item.Title, text)
                                    : item.PaymentMethod)
                        })
                        .ToList();
                }

        private string BuildDailyExpensePrompt(string text, string categoryLines)
                {
                    return $$"""
        Bạn là bộ phân tích ghi chép chi tiêu cá nhân.
        
        Hãy đọc câu người dùng nhập và trả về JSON hợp lệ.
        
        Danh mục chi tiêu hiện có:
        {{categoryLines}}
        
        Câu nhập:
        {{text}}
        
        Quy tắc:
        - Trích xuất tất cả khoản chi trong câu.
        - Không tự bịa khoản chi nếu câu không nói rõ.
        - amount là số tiền VND dạng number. Ví dụ 40k = 40000, 1.2tr = 1200000.
        - categoryName phải chọn một tên trong danh mục hiện có. Nếu không chắc, dùng "Khác".
        - paymentMethod chỉ được là "Cash" hoặc "Transfer".
        - Nếu người dùng không nhắc tới cách trả tiền thì paymentMethod="Cash".
        - Nếu câu có "thanh toán", "chuyển khoản", "quẹt thẻ", "thẻ", "bank", "ngân hàng", "Momo", "ZaloPay", "ví điện tử" hoặc tên ví/ngân hàng thì paymentMethod="Transfer".
        - title ngắn gọn, tự nhiên, không chứa số tiền.
        - Chỉ trả JSON, không markdown, không giải thích.
        
        JSON cần trả:
        {
          "items": [
            {
              "title": "Ăn sáng",
              "amount": 40000,
              "categoryName": "Ăn uống",
              "paymentMethod": "Cash"
            },
            {
              "title": "Đổ xăng",
              "amount": 50000,
              "categoryName": "Di chuyển",
              "paymentMethod": "Transfer"
            }
          ]
        }
        """;
                }

        public async Task<AiParsedFinancialNotificationDto> AnalyzeFinancialNotificationAsync(
                    Guid userId,
                    string? sourceApp,
                    string? title,
                    string body)
                {
                    var text = $"{title} {body}".Trim();
                    var lower = text.ToLower();
        
                    if (LooksPromotional(lower))
                    {
                        return new AiParsedFinancialNotificationDto
                        {
                            IsTransaction = false,
                            IgnoreReason = "Thông báo có dấu hiệu quảng cáo/khuyến mãi/OTP.",
                            Provider = sourceApp ?? "",
                            Confidence = 0.95m
                        };
                    }
        
                    var categories = await _context.ExpenseCategories
                        .Where(c => c.UserId == null || c.UserId == userId)
                        .OrderBy(c => c.Type)
                        .ThenBy(c => c.Name)
                        .Select(c => new { c.Name, c.Type })
                        .ToListAsync();
        
                    var categoryLines = string.Join(
                        "\n",
                        categories.Select(c => $"- {c.Name} ({c.Type})"));
                    var prompt = BuildFinancialNotificationPrompt(sourceApp, title, body, categoryLines);
                    var provider = _configuration["Ai:Provider"] ?? "Mock";
                    var openAiApiKey = _configuration["Ai:OpenAiApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
                    var geminiApiKey = _configuration["Ai:GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
        
                    AiParsedFinancialNotificationDto result;
                    try
                    {
                        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(geminiApiKey))
                        {
                            result = await AnalyzeFinancialNotificationWithGeminiAsync(prompt, geminiApiKey);
                        }
                        else if ((provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || provider.Equals("Orimise", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(openAiApiKey))
                        {
                            result = await AnalyzeFinancialNotificationWithOpenAiAsync(prompt, openAiApiKey);
                        }
                        else
                        {
                            throw new InvalidOperationException("Đã hết token, vui lòng liên hệ admin!");
                        }
                    }
                    catch (Exception ex)
                    {
                        var errMsg = ex.Message.Contains("admin") ? ex.Message : "Đã hết token, vui lòng liên hệ admin!";
                        throw new InvalidOperationException(errMsg, ex);
                    }
        
                    if (string.IsNullOrWhiteSpace(result.Provider))
                    {
                        result.Provider = sourceApp ?? "";
                    }
        
                    if (string.IsNullOrWhiteSpace(result.Currency))
                    {
                        result.Currency = "VND";
                    }
        
                    return result;
                }

        private string BuildFinancialNotificationPrompt(
                    string? sourceApp,
                    string? title,
                    string body,
                    string categoryLines)
                {
                    return $$"""
        Bạn là bộ phân tích thông báo giao dịch tài chính cá nhân.
        
        Nhiệm vụ: đọc thông báo từ ví điện tử/ngân hàng và trả về JSON hợp lệ.
        
        Danh mục hiện có:
        {{categoryLines}}
        
        Nguồn thông báo: {{sourceApp ?? "Không rõ"}}
        Tiêu đề thông báo: {{title ?? ""}}
        Nội dung thông báo:
        {{body}}
        
        Quy tắc rất quan trọng:
        - Nếu đây là quảng cáo, khuyến mãi, voucher, hoàn xu, nhắc nạp tiền, mời vay, thông báo hệ thống, bảo trì, OTP, đăng nhập, hoặc nội dung không phải biến động tiền thật thì trả isTransaction=false.
        - Chỉ nhận là giao dịch khi có dấu hiệu tiền thật đã cộng/trừ/thanh toán/chuyển/nhận/rút/hoàn tiền.
        - Không lấy số tài khoản, số dư, mã giao dịch, số điện thoại, OTP, ngày giờ làm amount.
        - Nếu thông báo có cả số tiền giao dịch và số dư, amount phải là số tiền giao dịch.
        - transactionType chỉ được là Expense, Income, Transfer, Refund, Unknown.
        - categoryName phải chọn một tên trong danh mục hiện có. Nếu không chắc, dùng "Khác".
        - confidence từ 0 đến 1.
        - Chỉ trả JSON, không giải thích, không markdown.
        
        JSON cần trả:
        {
          "isTransaction": true,
          "ignoreReason": "",
          "transactionType": "Expense",
          "amount": 50000,
          "currency": "VND",
          "title": "Circle K",
          "description": "Thanh toán tại Circle K",
          "categoryName": "Ăn uống",
          "provider": "{{sourceApp ?? ""}}",
          "accountLabel": "TK x1234",
          "confidence": 0.9
        }
        """;
                }

        private async Task<AiParsedFinancialNotificationDto> AnalyzeFinancialNotificationWithOpenAiAsync(string prompt, string apiKey)
                {
                    var model = _configuration["Ai:OpenAiModel"] ?? "gpt-4o-mini";
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(30);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        
                    var payload = new
                    {
                        model,
                        messages = new[]
                        {
                            new { role = "system", content = "Bạn chỉ trả về JSON hợp lệ theo yêu cầu của người dùng." },
                            new { role = "user", content = prompt }
                        },
                        temperature = 0.1,
                        response_format = new { type = "json_object" }
                    };
        
                    var baseUrl = _configuration["Ai:BaseUrl"] ?? "https://api.openai.com/v1";
                    var url = $"{baseUrl.TrimEnd('/')}/chat/completions";
                    var response = await client.PostAsJsonAsync(url, payload);
                    response.EnsureSuccessStatusCode();
        
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var content = document.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString();
        
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return new AiParsedFinancialNotificationDto
                        {
                            IsTransaction = false,
                            IgnoreReason = "AI không trả về nội dung phân tích."
                        };
                    }
        
                    return JsonSerializer.Deserialize<AiParsedFinancialNotificationDto>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new AiParsedFinancialNotificationDto
                        {
                            IsTransaction = false,
                            IgnoreReason = "Không đọc được JSON từ AI."
                        };
                }

        private async Task<AiParsedFinancialNotificationDto> AnalyzeFinancialNotificationWithGeminiAsync(string prompt, string apiKey)
                {
                    var content = await GenerateJsonWithGeminiAsync(prompt, apiKey);
        
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        return new AiParsedFinancialNotificationDto
                        {
                            IsTransaction = false,
                            IgnoreReason = "Gemini không trả về nội dung phân tích."
                        };
                    }
        
                    return JsonSerializer.Deserialize<AiParsedFinancialNotificationDto>(
                        content,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new AiParsedFinancialNotificationDto
                        {
                            IsTransaction = false,
                            IgnoreReason = "Không đọc được JSON từ Gemini."
                        };
                }

        private AiParsedFinancialNotificationDto AnalyzeFinancialNotificationLocally(string? sourceApp, string? title, string body)
                {
                    var text = $"{title} {body}".Trim();
                    var lower = text.ToLower();
        
                    if (LooksPromotional(lower))
                    {
                        return new AiParsedFinancialNotificationDto
                        {
                            IsTransaction = false,
                            IgnoreReason = "Thông báo có dấu hiệu quảng cáo/khuyến mãi.",
                            Provider = sourceApp ?? "",
                            Confidence = 0.8m
                        };
                    }
        
                    var amount = MoneyParser.Parse(text);
                    var type = DetectTransactionType(lower);
                    if (amount <= 0 || type == "Unknown")
                    {
                        return new AiParsedFinancialNotificationDto
                        {
                            IsTransaction = false,
                            IgnoreReason = "Không có dấu hiệu biến động tiền thật.",
                            Provider = sourceApp ?? "",
                            Confidence = 0.5m
                        };
                    }
        
                    var parsedItems = ParseExpenseItems(text);
                    var first = parsedItems.FirstOrDefault();
        
                    return new AiParsedFinancialNotificationDto
                    {
                        IsTransaction = true,
                        TransactionType = type,
                        Amount = amount,
                        Currency = "VND",
                        Title = first?.Title ?? ExtractFallbackTitle(text),
                        Description = first?.Title ?? ExtractFallbackTitle(text),
                        CategoryName = first?.CategoryName ?? "Khác",
                        Provider = sourceApp ?? "",
                        AccountLabel = ExtractAccountLabel(text),
                        Confidence = 0.65m
                    };
                }

        private bool LooksPromotional(string text)
                {
                    var absolutePromoKeywords = new[]
                    {
                        "xu về", "xu ve", "xu đã về", "xu da ve", "lưu mã", "luu ma", "nhập mã", "nhap ma",
                        "mã giảm", "ma giam", "voucher", "coupon", "đổi quà", "doi qua",
                        "quà ngay", "qua ngay", "mời vay", "mở thẻ", "mo the", "nhận quà", "nhan qua",
                        "otp", "ma xac thuc", "mã xác thực", "dang nhap", "đăng nhập",
                        "lưu thêm", "luu them", "đơn tiktok", "don tiktok", "giảm tới", "giam toi",
                        "đổi xu", "doi xu", "chúc mừng", "chuc mung", "trúng thưởng", "trung thuong",
                        "may mắn", "may man", "nhấp để đổi", "nhap de doi", "ưu đãi", "uu dai",
                        "khuyến mãi", "khuyen mai", "quảng cáo", "quang cao", "tặng bạn", "tang ban",
                        "hoàn xu", "hoan xu", "tích điểm", "tich diem", "giảm giá", "giam gia",
                        "giảm thêm", "giam them"
                    };
        
                    if (absolutePromoKeywords.Any(text.Contains))
                    {
                        return true;
                    }
        
                    var promoKeywords = new[]
                    {
                        "cashback", "mời bạn", "sale", "flash sale", "bao tri", "bảo trì"
                    };
        
                    var transactionKeywords = new[]
                    {
                        "thanh toán", "thanh toan", "chuyển tiền", "chuyen tien", "nhận tiền",
                        "nhan tien", "rút tiền", "rut tien", "phát sinh", "phat sinh",
                        "ghi nợ", "ghi no", "ghi có", "ghi co", "trừ", "tru", "cộng", "cong"
                    };
        
                    return promoKeywords.Any(text.Contains) && !transactionKeywords.Any(text.Contains);
                }

        private string DetectTransactionType(string lowerText)
                {
                    if (lowerText.Contains("+") || lowerText.Contains("nhận") || lowerText.Contains("nhan") ||
                        lowerText.Contains("cộng") || lowerText.Contains("cong") || lowerText.Contains("ghi có") ||
                        lowerText.Contains("ghi co") || lowerText.Contains("hoàn tiền") || lowerText.Contains("hoan tien"))
                    {
                        return lowerText.Contains("hoàn tiền") || lowerText.Contains("hoan tien") ? "Refund" : "Income";
                    }
        
                    if (lowerText.Contains("-") || lowerText.Contains("thanh toán") || lowerText.Contains("thanh toan") ||
                        lowerText.Contains("trừ") || lowerText.Contains("tru") || lowerText.Contains("ghi nợ") ||
                        lowerText.Contains("ghi no") || lowerText.Contains("rút") || lowerText.Contains("rut") ||
                        lowerText.Contains("chuyển") || lowerText.Contains("chuyen"))
                    {
                        return "Expense";
                    }
        
                    return "Unknown";
                }

        private string ExtractFallbackTitle(string text)
                {
                    var match = Regex.Match(text, @"(?:tai|tại|den|đến|thanh toán)\s+(?<name>.+?)\s+(?:luc|lúc|ngay|ngày|vao|vào|$)", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return CleanTitle(match.Groups["name"].Value);
                    }
        
                    return "Giao dịch tài chính";
                }

        private string ExtractAccountLabel(string text)
                {
                    var match = Regex.Match(text, @"\b(?:tk|stk)\s*([A-Za-z]?\d{3,})\b", RegexOptions.IgnoreCase);
                    return match.Success ? $"TK {match.Groups[1].Value}" : string.Empty;
                }

        private List<ParsedExpenseInfo> ParseExpenseItems(string text)
                {
                    var results = new List<ParsedExpenseInfo>();
                    var parts = Regex.Split(text, @",|;|\s+và\s+", RegexOptions.IgnoreCase);
                    var defaultPaymentMethod = DetectPaymentMethod(text, null) ?? "Cash";
        
                    foreach (var part in parts)
                    {
                        if (string.IsNullOrWhiteSpace(part)) continue;
        
                        var amount = MoneyParser.Parse(part);
                        if (amount <= 0) continue;
        
                        var title = ExtractExpenseTitle(part);
                        var categoryName = MapCategoryFromTitle(title);
                        var categoryType = (categoryName == "Lương" || categoryName == "Thu nhập khác") ? "Income" : "Expense";
        
                        results.Add(new ParsedExpenseInfo
                        {
                            Title = title,
                            Amount = amount,
                            CategoryName = categoryName,
                            CategoryType = categoryType,
                            PaymentMethod = NormalizeAiPaymentMethod(DetectPaymentMethod(part, null) ?? defaultPaymentMethod)
                        });
                    }
        
                    return results;
                }

        private static string NormalizeAiPaymentMethod(string? paymentMethod)
                {
                    return string.Equals(paymentMethod, "Transfer", StringComparison.OrdinalIgnoreCase) ? "Transfer" : "Cash";
                }

        private string? DetectPaymentMethod(string text, string? fallbackText)
                {
                    var combined = $"{text} {fallbackText}".Trim();
                    var normalized = RemoveDiacritics(combined.ToLowerInvariant());
        
                    var cashKeywords = new[]
                    {
                        "tien mat", "tra tien mat", "dua tien mat", "cash"
                    };
        
                    if (cashKeywords.Any(k => normalized.Contains(k)))
                    {
                        return "Cash";
                    }
        
                    var transferKeywords = new[]
                    {
                        "thanh toan", "chuyen khoan", "chuyen tien", "quet the", "the ngan hang",
                        "the tin dung", "the atm", "atm", "bank", "ngan hang", "internet banking",
                        "mobile banking", "momo", "zalopay", "zalo pay", "vi dien tu", "vi momo",
                        "qr", "ma qr", "stk", "tai khoan"
                    };
        
                    return transferKeywords.Any(k => normalized.Contains(k)) ? "Transfer" : null;
                }

        private string ExtractExpenseTitle(string part)
                {
                    var title = Regex.Replace(part, @"[+-]?\s*\d+(?:[.,]\d+)?\s*(k|đ|d|vnd|vnđ)?\b", "", RegexOptions.IgnoreCase).Trim();
                    title = Regex.Replace(title, @"\b(hôm nay|ngày mai|hồi sáng|hồi trưa|chiều nay|tối nay|tiền|chi|tiêu|mua|đã|tk|stk|vnd)\b", "", RegexOptions.IgnoreCase).Trim();
                    title = Regex.Replace(title, @"\s+", " ");
        
                    if (string.IsNullOrEmpty(title))
                    {
                        title = "Chi tiêu khác";
                    }
        
                    return CleanTitle(title);
                }

        private string CleanTitle(string title)
                {
                    title = Regex.Replace(title, @"\s+", " ").Trim(' ', '.', '-', ':');
                    if (string.IsNullOrWhiteSpace(title)) return "Chi tiêu khác";
                    return char.ToUpper(title[0]) + title.Substring(1);
                }

        private string MapCategoryFromTitle(string title)
                {
                    var titleLower = title.ToLower();
        
                    if (titleLower.Contains("lương") || titleLower.Contains("luong") || titleLower.Contains("thu nhập") ||
                        titleLower.Contains("thu nhap") || titleLower.Contains("salary"))
                    {
                        return "Lương";
                    }
        
                    if (titleLower.Contains("được cho") || titleLower.Contains("duoc cho") || titleLower.Contains("cho tiền") ||
                        titleLower.Contains("cho tien") || titleLower.Contains("tặng") || titleLower.Contains("tang") ||
                        titleLower.Contains("lì xì") || titleLower.Contains("li xi") || 
                        titleLower.Contains("cho tôi") || titleLower.Contains("cho toi") ||
                        titleLower.Contains("cho mình") || titleLower.Contains("cho minh") ||
                        titleLower.Contains("cho con") || titleLower.Contains("bố mẹ cho") ||
                        titleLower.Contains("bo me cho"))
                    {
                        return "Thu nhập khác";
                    }
        
                    if (titleLower.Contains("ăn") || titleLower.Contains("uống") || titleLower.Contains("phở") ||
                        titleLower.Contains("cà phê") || titleLower.Contains("cafe") || titleLower.Contains("cơm") ||
                        titleLower.Contains("bánh") || titleLower.Contains("sữa") || titleLower.Contains("circle k") ||
                        titleLower.Contains("highlands"))
                    {
                        return "Ăn uống";
                    }
        
                    if (titleLower.Contains("xăng") || titleLower.Contains("grab") || titleLower.Contains("xe") ||
                        titleLower.Contains("bus") || titleLower.Contains("taxi") || titleLower.Contains("di chuyển"))
                    {
                        return "Di chuyển";
                    }
        
                    if (titleLower.Contains("học") || titleLower.Contains("sách") || titleLower.Contains("vở") ||
                        titleLower.Contains("khóa học") || titleLower.Contains("study") || titleLower.Contains("thi"))
                    {
                        return "Học tập";
                    }
        
                    if (titleLower.Contains("game") || titleLower.Contains("phim") || titleLower.Contains("chơi") ||
                        titleLower.Contains("hát") || titleLower.Contains("karaoke") || titleLower.Contains("du lịch"))
                    {
                        return "Giải trí";
                    }
        
                    if (titleLower.Contains("nhà") || titleLower.Contains("phòng") || titleLower.Contains("điện") ||
                        titleLower.Contains("nước") || titleLower.Contains("mạng") || titleLower.Contains("internet"))
                    {
                        return "Tiền nhà";
                    }
        
                    if (titleLower.Contains("thuốc") || titleLower.Contains("khám") || titleLower.Contains("bệnh") ||
                        titleLower.Contains("sức khỏe") || titleLower.Contains("bảo hiểm") || titleLower.Contains("viện"))
                    {
                        return "Sức khỏe";
                    }
        
                    if (titleLower.Contains("mua") || titleLower.Contains("áo") || titleLower.Contains("quần") ||
                        titleLower.Contains("giày") || titleLower.Contains("shopee") || titleLower.Contains("shopping") ||
                        titleLower.Contains("sắm"))
                    {
                        return "Mua sắm";
                    }
        
                    return "Khác";
                }

        private async Task<ExpenseCategory?> FindCategoryByNameAndTypeAsync(Guid userId, string categoryName, string type)
                {
                    return await _context.ExpenseCategories
                        .FirstOrDefaultAsync(c => (c.UserId == null || c.UserId == userId) &&
                                                  c.Name.ToLower() == categoryName.ToLower() &&
                                                  c.Type == type);
                }

        private class ParsedExpenseInfo
                {
                    public string Title { get; set; } = string.Empty;
                    public decimal Amount { get; set; }
                    public string CategoryName { get; set; } = string.Empty;
                    public string CategoryType { get; set; } = "Expense";
                    public string PaymentMethod { get; set; } = "Cash";
                }

        private class AiDailyExpenseParseResult
                {
                    public List<AiDailyExpenseItem> Items { get; set; } = new();
                }

        private class AiDailyExpenseItem
                {
                    public string Title { get; set; } = string.Empty;
                    public decimal Amount { get; set; }
                    public string CategoryName { get; set; } = string.Empty;
                    public string CategoryType { get; set; } = "Expense";
                    public string PaymentMethod { get; set; } = "Cash";
                }
    }
}
