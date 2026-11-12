using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiDailyExpense.Api.DTOs.Ai;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface IAiService
    {
        Task<AiParseResponse> ParseAsync(Guid userId, AiParseRequest request);
        Task<AiTimetableParseResponse> ParseTimetableImageAsync(Guid userId, byte[] imageBytes, string mimeType);
        Task<AiVocabularyResponse> AnalyzeVocabularyAsync(Guid userId, AiVocabularyRequest request);
        Task<AiVocabularyAnalyzeResponse> AnalyzeVocabularyExtendedAsync(Guid userId, AiVocabularyAnalyzeRequest request);
        Task<List<ParsedExpenseItemDto>> AnalyzeExpenseTextAsync(Guid userId, string text);
        Task<AiParsedFinancialNotificationDto> AnalyzeFinancialNotificationAsync(
            Guid userId,
            string? sourceApp,
            string? title,
            string body);
    }
}
