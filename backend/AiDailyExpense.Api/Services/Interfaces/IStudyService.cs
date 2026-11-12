using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiDailyExpense.Api.DTOs.Study;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface IStudyService
    {
        Task<List<StudyDeckDto>> GetDecksAsync(Guid userId);
        Task<StudyDeckDto> CreateDeckAsync(Guid userId, CreateStudyDeckRequest request);
        Task<bool> DeleteDeckAsync(Guid userId, int deckId);
        Task<List<StudyCardDto>> GetCardsAsync(Guid userId, int? deckId);
        Task<StudyCardDto> CreateCardAsync(Guid userId, CreateStudyCardRequest request);
        Task<bool> DeleteCardAsync(Guid userId, long cardId);
        Task<List<StudyCardDto>> GetDueCardsAsync(Guid userId, int? deckId);
        Task<StudyReviewResultDto> ReviewCardAsync(Guid userId, long cardId, ReviewStudyCardRequest request);
        Task<StudyStatsDto> GetStatsAsync(Guid userId);
    }
}
