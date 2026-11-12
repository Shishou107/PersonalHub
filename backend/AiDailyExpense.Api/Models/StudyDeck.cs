using System;
using System.Collections.Generic;

namespace AiDailyExpense.Api.Models
{
    public class StudyDeck
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Language { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User User { get; set; } = null!;
        public ICollection<StudyCard> StudyCards { get; set; } = new List<StudyCard>();
    }
}
