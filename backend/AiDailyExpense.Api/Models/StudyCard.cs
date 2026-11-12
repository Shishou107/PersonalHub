using System;
using System.Collections.Generic;

namespace AiDailyExpense.Api.Models
{
    public class StudyCard
    {
        public long Id { get; set; }
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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        public StudyDeck Deck { get; set; } = null!;
        public ICollection<StudyReview> StudyReviews { get; set; } = new List<StudyReview>();
    }
}
