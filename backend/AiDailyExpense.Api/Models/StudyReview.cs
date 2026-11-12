using System;

namespace AiDailyExpense.Api.Models
{
    public class StudyReview
    {
        public long Id { get; set; }
        public long CardId { get; set; }
        public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
        public string Rating { get; set; } = string.Empty;
        public DateTime NextReviewDate { get; set; }
        public int IntervalDays { get; set; }
        public decimal EaseFactor { get; set; } = 2.5m;
        public int ReviewCount { get; set; }

        public StudyCard Card { get; set; } = null!;
    }
}
