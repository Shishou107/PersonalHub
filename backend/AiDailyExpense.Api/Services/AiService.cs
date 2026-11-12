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
        private static readonly TimeSpan DefaultScheduleDuration = TimeSpan.FromMinutes(15);

        private static readonly TimeSpan AllDayStartTime = new TimeSpan(0, 1, 0);

        private static readonly TimeSpan AllDayEndTime = new TimeSpan(23, 59, 0);

        private readonly AppDbContext _context;

        private readonly IExpenseService _expenseService;

        private readonly ITaskService _taskService;

        private readonly IHttpClientFactory _httpClientFactory;

        private readonly IConfiguration _configuration;

        public AiService(
                    AppDbContext context,
                    IExpenseService expenseService,
                    ITaskService taskService,
                    IHttpClientFactory httpClientFactory,
                    IConfiguration configuration)
                {
                    _context = context;
                    _expenseService = expenseService;
                    _taskService = taskService;
                    _httpClientFactory = httpClientFactory;
                    _configuration = configuration;
                }

        public async Task<AiParseResponse> ParseAsync(Guid userId, AiParseRequest request)
                {
                    var text = request.Text.Trim();
                    var log = new AiCommandLog
                    {
                        UserId = userId,
                        RawInput = text,
                        CreatedAt = DateTime.UtcNow
                    };
        
                    var response = new AiParseResponse { Intent = "unknown" };
        
                    try
                    {
                        var provider = _configuration["Ai:Provider"] ?? "Mock";
                        var geminiApiKey = _configuration["Ai:GeminiApiKey"] ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";
                        var openAiApiKey = _configuration["Ai:OpenAiApiKey"] ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "";
        
                        if (provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(geminiApiKey))
                        {
                            response = await ParseWithGeminiAsync(userId, text, geminiApiKey);
                        }
                        else if ((provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) || provider.Equals("Orimise", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(openAiApiKey))
                        {
                            response = await ParseWithOpenAiAsync(userId, text, openAiApiKey);
                        }
                        else
                        {
                            throw new InvalidOperationException("Đã hết token, vui lòng liên hệ admin!");
                        }
        
                        log.Intent = response.Intent;
                        log.ParsedJson = JsonSerializer.Serialize(response);
                        log.IsSuccess = response.Intent != "unknown";
                    }
                    catch (Exception ex)
                    {
                        log.IsSuccess = false;
                        log.ErrorMessage = ex.Message;
                        var errMsg = ex.Message.Contains("admin") ? ex.Message : "Đã hết token, vui lòng liên hệ admin!";
                        throw new InvalidOperationException(errMsg, ex);
                    }
        
                    _context.AiCommandLogs.Add(log);
                    await _context.SaveChangesAsync();
        
                    return response;
                }

        private async Task<string> GenerateJsonWithGeminiAsync(string prompt, string apiKey)
                {
                    var configuredModel = _configuration["Ai:GeminiModel"]?.Trim();
                    var candidateModels = new List<string>();
                    if (!string.IsNullOrWhiteSpace(configuredModel))
                    {
                        candidateModels.Add(configuredModel);
                    }
                    candidateModels.AddRange(new[] { "gemini-3.1-flash-lite", "gemini-2.5-flash-lite", "gemini-2.0-flash", "gemini-1.5-flash" });
                    candidateModels = candidateModels.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(30);
        
                    var payload = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                role = "user",
                                parts = new[]
                                {
                                    new { text = prompt }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature = 0.1,
                            responseMimeType = "application/json"
                        }
                    };
        
                    foreach (var model in candidateModels)
                    {
                        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";
                        
                        int maxRetries = 2;
                        for (int i = 0; i < maxRetries; i++)
                        {
                            try
                            {
                                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, url)
                                {
                                    Content = JsonContent.Create(payload)
                                };
                                requestMsg.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        
                                var response = await client.SendAsync(requestMsg);
                                if (response.IsSuccessStatusCode)
                                {
                                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                                    var raw = document.RootElement
                                        .GetProperty("candidates")[0]
                                        .GetProperty("content")
                                        .GetProperty("parts")[0]
                                        .GetProperty("text")
                                        .GetString() ?? "";
                                    return CleanJsonContent(raw);
                                }
        
                                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    // Model name not found on Google AI, try next candidate model
                                    break;
                                }
        
                                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || 
                                    (int)response.StatusCode == 429)
                                {
                                    if (i < maxRetries - 1)
                                    {
                                        await Task.Delay(1000 * (i + 1));
                                        continue;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                if (i == maxRetries - 1)
                                {
                                    break;
                                }
                                await Task.Delay(1000 * (i + 1));
                            }
                        }
                    }
        
                    throw new InvalidOperationException("Không thể kết nối đến Gemini API.");
                }

        private async Task<string> GenerateJsonWithGeminiImageAsync(string prompt, byte[] imageBytes, string mimeType, string apiKey)
                {
                    var configuredModel = _configuration["Ai:GeminiModel"]?.Trim();
                    var candidateModels = new List<string>();
                    if (!string.IsNullOrWhiteSpace(configuredModel))
                    {
                        candidateModels.Add(configuredModel);
                    }
                    candidateModels.AddRange(new[] { "gemini-3.1-flash-lite", "gemini-2.5-flash-lite", "gemini-2.0-flash", "gemini-1.5-flash" });
                    candidateModels = candidateModels.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(60);
        
                    var payload = new
                    {
                        contents = new[]
                        {
                            new
                            {
                                role = "user",
                                parts = new object[]
                                {
                                    new { text = prompt },
                                    new
                                    {
                                        inlineData = new
                                        {
                                            mimeType,
                                            data = Convert.ToBase64String(imageBytes)
                                        }
                                    }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature = 0.1,
                            responseMimeType = "application/json"
                        }
                    };
        
                    foreach (var model in candidateModels)
                    {
                        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";
                        
                        int maxRetries = 2;
                        for (int i = 0; i < maxRetries; i++)
                        {
                            try
                            {
                                using var requestMsg = new HttpRequestMessage(HttpMethod.Post, url)
                                {
                                    Content = JsonContent.Create(payload)
                                };
                                requestMsg.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);
        
                                var response = await client.SendAsync(requestMsg);
                                if (response.IsSuccessStatusCode)
                                {
                                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                                    var raw = document.RootElement
                                        .GetProperty("candidates")[0]
                                        .GetProperty("content")
                                        .GetProperty("parts")[0]
                                        .GetProperty("text")
                                        .GetString() ?? "";
                                    return CleanJsonContent(raw);
                                }
        
                                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                                {
                                    // Model name not found on Google AI, try next candidate model
                                    break;
                                }
        
                                if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || 
                                    (int)response.StatusCode == 429)
                                {
                                    if (i < maxRetries - 1)
                                    {
                                        await Task.Delay(1000 * (i + 1));
                                        continue;
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                if (i == maxRetries - 1)
                                {
                                    break;
                                }
                                await Task.Delay(1000 * (i + 1));
                            }
                        }
                    }
        
                    throw new InvalidOperationException("Không thể kết nối đến Gemini API.");
                }

        private async Task<string> GenerateJsonWithOpenAiAsync(string prompt, string apiKey)
                {
                    var model = _configuration["Ai:OpenAiModel"] ?? "gpt-4o-mini";
                    var baseUrl = _configuration["Ai:BaseUrl"] ?? "https://api.openai.com/v1";
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
        
                    var url = $"{baseUrl.TrimEnd('/')}/chat/completions";
                    
                    int maxRetries = 3;
                    System.Net.Http.HttpResponseMessage? response = null;
                    
                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            response = await client.PostAsJsonAsync(url, payload);
                            if (response.IsSuccessStatusCode)
                            {
                                break;
                            }
                            
                            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || 
                                (int)response.StatusCode == 429)
                            {
                                if (i < maxRetries - 1)
                                {
                                    await Task.Delay(1000 * (i + 1));
                                    continue;
                                }
                            }
                            
                            response.EnsureSuccessStatusCode();
                        }
                        catch (Exception)
                        {
                            if (i == maxRetries - 1)
                            {
                                if (response != null) response.EnsureSuccessStatusCode();
                                throw;
                            }
                            await Task.Delay(1000 * (i + 1));
                        }
                    }
        
                    if (response == null)
                    {
                        throw new InvalidOperationException("Không thể kết nối đến OpenAI/Orimise API.");
                    }
        
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    var raw = document.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "";
                    return CleanJsonContent(raw);
                }

        private static string CleanJsonContent(string content)
                {
                    if (string.IsNullOrWhiteSpace(content)) return "";
                    var trimmed = content.Trim();
                    
                    if (trimmed.StartsWith("```"))
                    {
                        var startIndex = trimmed.IndexOf('\n');
                        if (startIndex != -1)
                        {
                            trimmed = trimmed.Substring(startIndex + 1);
                        }
                        else
                        {
                            trimmed = trimmed.TrimStart('`');
                        }
                        
                        var lastBackticks = trimmed.LastIndexOf("```");
                        if (lastBackticks != -1)
                        {
                            trimmed = trimmed.Substring(0, lastBackticks);
                        }
                        trimmed = trimmed.Trim();
                    }
                    
                    var firstBrace = trimmed.IndexOfAny(new[] { '{', '[' });
                    var lastBrace = trimmed.LastIndexOfAny(new[] { '}', ']' });
                    if (firstBrace >= 0 && lastBrace > firstBrace)
                    {
                        trimmed = trimmed.Substring(firstBrace, lastBrace - firstBrace + 1);
                    }
                    
                    return trimmed;
                }

        private async Task<string> GenerateJsonWithOpenAiImageAsync(string prompt, byte[] imageBytes, string mimeType, string apiKey)
                {
                    var model = _configuration["Ai:OpenAiModel"] ?? "gpt-4o-mini";
                    var baseUrl = _configuration["Ai:BaseUrl"] ?? "https://api.openai.com/v1";
                    var client = _httpClientFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(60);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        
                    var base64Image = Convert.ToBase64String(imageBytes);
                    var payload = new
                    {
                        model,
                        messages = new object[]
                        {
                            new
                            {
                                role = "user",
                                content = new object[]
                                {
                                    new { type = "text", text = prompt },
                                    new
                                    {
                                        type = "image_url",
                                        image_url = new
                                        {
                                            url = $"data:{mimeType};base64,{base64Image}"
                                        }
                                    }
                                }
                            }
                        },
                        temperature = 0.1,
                        response_format = new { type = "json_object" }
                    };
        
                    var url = $"{baseUrl.TrimEnd('/')}/chat/completions";
                    
                    int maxRetries = 3;
                    System.Net.Http.HttpResponseMessage? response = null;
                    
                    for (int i = 0; i < maxRetries; i++)
                    {
                        try
                        {
                            response = await client.PostAsJsonAsync(url, payload);
                            if (response.IsSuccessStatusCode)
                            {
                                break;
                            }
                            
                            if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable || 
                                (int)response.StatusCode == 429)
                            {
                                if (i < maxRetries - 1)
                                {
                                    await Task.Delay(1000 * (i + 1));
                                    continue;
                                }
                            }
                            
                            response.EnsureSuccessStatusCode();
                        }
                        catch (Exception)
                        {
                            if (i == maxRetries - 1)
                            {
                                if (response != null) response.EnsureSuccessStatusCode();
                                throw;
                            }
                            await Task.Delay(1000 * (i + 1));
                        }
                    }
        
                    if (response == null)
                    {
                        throw new InvalidOperationException("Không thể kết nối đến OpenAI/Orimise API.");
                    }
        
                    using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    return document.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("message")
                        .GetProperty("content")
                        .GetString() ?? "";
                }

        private DateTime GetVietnamNow()
                {
                    try
                    {
                        var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone);
                    }
                    catch
                    {
                        return DateTime.UtcNow.AddHours(7);
                    }
                }

        private string RemoveDiacritics(string text)
                {
                    var normalized = text.Normalize(NormalizationForm.FormD);
                    var builder = new StringBuilder(normalized.Length);
        
                    foreach (var c in normalized)
                    {
                        var category = CharUnicodeInfo.GetUnicodeCategory(c);
                        if (category != UnicodeCategory.NonSpacingMark)
                        {
                            builder.Append(c);
                        }
                    }
        
                    return builder
                        .ToString()
                        .Normalize(NormalizationForm.FormC)
                        .Replace('\u0111', 'd')
                        .Replace('\u0110', 'D')
                        .Replace('đ', 'd')
                        .Replace('Đ', 'D');
                }

        private class UnifiedAiParseResult
                {
                    public string Intent { get; set; } = string.Empty;
                    public List<AiDailyExpenseItem>? ExpenseItems { get; set; }
                    public AiParsedTaskResult? Task { get; set; }
                    public List<AiParsedTaskResult>? Tasks { get; set; }
                }
    }
}
