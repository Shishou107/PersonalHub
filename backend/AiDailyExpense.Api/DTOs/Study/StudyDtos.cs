using System;
using System.Collections.Generic;

namespace AiDailyExpense.Api.DTOs.Study
{
    public class StudyDeckDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Language { get; set; } = string.Empty;
        public int CardCount { get; set; }
        public int DueCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateStudyDeckRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Language { get; set; }
    }

    public class StudyCardDto
    {
        public long Id { get; set; }
        public int DeckId { get; set; }
        public string DeckName { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string FrontText { get; set; } = string.Empty;
        public string BackText { get; set; } = string.Empty;
        public string? Example { get; set; }
        public string? Note { get; set; }
        public string? Pronunciation { get; set; }
        public string? Tags { get; set; }
        public string? ExtendedData { get; set; }
        public string? NormalizedText { get; set; }
        public string Difficulty { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? LastReviewDate { get; set; }
        public DateTime? NextReviewDate { get; set; }
        public int IntervalDays { get; set; }
        public int ReviewCount { get; set; }
        public string? LastRating { get; set; }
    }

    public class CreateStudyCardRequest
    {
        public int DeckId { get; set; }
        public string CardType { get; set; } = "Vocabulary";
        public string FrontText { get; set; } = string.Empty;
        public string BackText { get; set; } = string.Empty;
        public string? Example { get; set; }
        public string? Note { get; set; }
        public string? Pronunciation { get; set; }
        public string? Tags { get; set; }
        public string? ExtendedData { get; set; }
        public string? NormalizedText { get; set; }
        public string Difficulty { get; set; } = "New";
    }

    public class ReviewStudyCardRequest
    {
        public string Rating { get; set; } = string.Empty;
    }

    public class StudyReviewResultDto
    {
        public long CardId { get; set; }
        public string Rating { get; set; } = string.Empty;
        public int IntervalDays { get; set; }
        public DateTime NextReviewDate { get; set; }
        public int ReviewCount { get; set; }
    }

    public class StudyStatsDto
    {
        public int DueToday { get; set; }
        public int ReviewedToday { get; set; }
        public int TotalCards { get; set; }
        public int WeakCards { get; set; }
        public int UpcomingCards { get; set; }
        public int StreakDays { get; set; }
        public List<StudyDeckDto> Decks { get; set; } = new();
    }
}
