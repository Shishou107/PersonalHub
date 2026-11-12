using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using AiDailyExpense.Api.Data;
using AiDailyExpense.Api.DTOs.Study;
using AiDailyExpense.Api.Models;
using AiDailyExpense.Api.Services.Interfaces;

namespace AiDailyExpense.Api.Services
{
    public class StudyService : IStudyService
    {
        private readonly AppDbContext _context;

        public StudyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<List<StudyDeckDto>> GetDecksAsync(Guid userId)
        {
            var dueCards = await GetDueCardIdsAsync(userId);

            return await _context.StudyDecks
                .Where(d => d.UserId == userId)
                .OrderBy(d => d.Name)
                .Select(d => new StudyDeckDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    Language = ResolveDeckLanguage(d.Language, d.Name, d.Description),
                    CardCount = d.StudyCards.Count,
                    DueCount = d.StudyCards.Count(c => dueCards.Contains(c.Id)),
                    CreatedAt = d.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<StudyDeckDto> CreateDeckAsync(Guid userId, CreateStudyDeckRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                throw new ArgumentException("Ten bo the khong duoc de trong.");
            }

            var deck = new StudyDeck
            {
                UserId = userId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                Language = ResolveDeckLanguage(request.Language, request.Name, request.Description),
                CreatedAt = DateTime.UtcNow
            };

            _context.StudyDecks.Add(deck);
            await _context.SaveChangesAsync();

            return new StudyDeckDto
            {
                Id = deck.Id,
                Name = deck.Name,
                Description = deck.Description,
                Language = deck.Language,
                CardCount = 0,
                DueCount = 0,
                CreatedAt = deck.CreatedAt
            };
        }

        public async Task<bool> DeleteDeckAsync(Guid userId, int deckId)
        {
            var deck = await _context.StudyDecks.FirstOrDefaultAsync(d => d.Id == deckId && d.UserId == userId);
            if (deck == null)
            {
                return false;
            }

            _context.StudyDecks.Remove(deck);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<StudyCardDto>> GetCardsAsync(Guid userId, int? deckId)
        {
            var query = _context.StudyCards
                .Include(c => c.Deck)
                .Include(c => c.StudyReviews)
                .Where(c => c.Deck.UserId == userId);

            if (deckId.HasValue)
            {
                query = query.Where(c => c.DeckId == deckId.Value);
            }

            var cards = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return cards.Select(ToCardDto).ToList();
        }

        public async Task<StudyCardDto> CreateCardAsync(Guid userId, CreateStudyCardRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FrontText) || string.IsNullOrWhiteSpace(request.BackText))
            {
                throw new ArgumentException("Mat truoc va mat sau khong duoc de trong.");
            }

            var deck = await _context.StudyDecks.FirstOrDefaultAsync(d => d.Id == request.DeckId && d.UserId == userId);
            if (deck == null)
            {
                throw new KeyNotFoundException("Khong tim thay bo the.");
            }

            var normalizedText = NormalizeStudyText(request.NormalizedText ?? request.FrontText);
            var duplicateExists = await _context.StudyCards
                .AnyAsync(c => c.DeckId == request.DeckId &&
                    (c.NormalizedText == normalizedText || c.FrontText.ToLower() == normalizedText));

            if (duplicateExists)
            {
                throw new ArgumentException("The hoc nay da ton tai trong bo the.");
            }

            var card = new StudyCard
            {
                DeckId = request.DeckId,
                CardType = NormalizeCardType(request.CardType),
                FrontText = request.FrontText.Trim(),
                BackText = request.BackText.Trim(),
                Example = request.Example?.Trim(),
                Note = request.Note?.Trim(),
                Pronunciation = request.Pronunciation?.Trim(),
                Tags = request.Tags?.Trim(),
                ExtendedData = request.ExtendedData?.Trim(),
                NormalizedText = normalizedText,
                Difficulty = string.IsNullOrWhiteSpace(request.Difficulty) ? "New" : request.Difficulty.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.StudyCards.Add(card);
            await _context.SaveChangesAsync();

            card.Deck = deck;
            return ToCardDto(card);
        }

        public async Task<bool> DeleteCardAsync(Guid userId, long cardId)
        {
            var card = await _context.StudyCards
                .Include(c => c.Deck)
                .FirstOrDefaultAsync(c => c.Id == cardId && c.Deck.UserId == userId);

            if (card == null)
            {
                return false;
            }

            _context.StudyCards.Remove(card);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<StudyCardDto>> GetDueCardsAsync(Guid userId, int? deckId)
        {
            var dueCardIds = await GetDueCardIdsAsync(userId, deckId);

            var query = _context.StudyCards
                .Include(c => c.Deck)
                .Include(c => c.StudyReviews)
                .Where(c => c.Deck.UserId == userId && dueCardIds.Contains(c.Id));

            if (deckId.HasValue)
            {
                query = query.Where(c => c.DeckId == deckId.Value);
            }

            var cards = await query
                .OrderBy(c => c.StudyReviews.Any() ? 1 : 0)
                .ThenBy(c => c.StudyReviews.OrderByDescending(r => r.ReviewDate).Select(r => r.NextReviewDate).FirstOrDefault())
                .ThenBy(c => c.CreatedAt)
                .Take(50)
                .ToListAsync();

            return cards.Select(ToCardDto).ToList();
        }

        public async Task<StudyReviewResultDto> ReviewCardAsync(Guid userId, long cardId, ReviewStudyCardRequest request)
        {
            var rating = NormalizeRating(request.Rating);
            var card = await _context.StudyCards
                .Include(c => c.Deck)
                .Include(c => c.StudyReviews)
                .FirstOrDefaultAsync(c => c.Id == cardId && c.Deck.UserId == userId);

            if (card == null)
            {
                throw new KeyNotFoundException("Khong tim thay the hoc.");
            }

            var latest = card.StudyReviews.OrderByDescending(r => r.ReviewDate).FirstOrDefault();
            var oldInterval = latest?.IntervalDays ?? 0;
            var intervalDays = CalculateIntervalDays(rating, oldInterval);
            var easeFactor = CalculateEaseFactor(rating, latest?.EaseFactor ?? 2.5m);
            var reviewCount = (latest?.ReviewCount ?? 0) + 1;
            var nextReviewDate = CalculateNextReviewDate(rating, intervalDays);

            var review = new StudyReview
            {
                CardId = card.Id,
                ReviewDate = DateTime.UtcNow,
                Rating = rating,
                NextReviewDate = nextReviewDate,
                IntervalDays = intervalDays,
                EaseFactor = easeFactor,
                ReviewCount = reviewCount
            };

            card.Difficulty = rating == "Forgot" || rating == "Hard" ? "Weak" : rating;
            card.UpdatedAt = DateTime.UtcNow;

            _context.StudyReviews.Add(review);
            await _context.SaveChangesAsync();

            return new StudyReviewResultDto
            {
                CardId = card.Id,
                Rating = rating,
                IntervalDays = intervalDays,
                NextReviewDate = nextReviewDate,
                ReviewCount = reviewCount
            };
        }

        public async Task<StudyStatsDto> GetStatsAsync(Guid userId)
        {
            var decks = await GetDecksAsync(userId);
            var todayUtc = DateTime.UtcNow.Date;
            var tomorrowUtc = todayUtc.AddDays(1);
            var nextWeekUtc = todayUtc.AddDays(7);
            var dueCardIds = await GetDueCardIdsAsync(userId);

            var cards = _context.StudyCards.Where(c => c.Deck.UserId == userId);
            var reviewedToday = await _context.StudyReviews
                .CountAsync(r => r.Card.Deck.UserId == userId && r.ReviewDate >= todayUtc && r.ReviewDate < tomorrowUtc);

            var latestReviews = await _context.StudyCards
                .Where(c => c.Deck.UserId == userId)
                .Select(c => c.StudyReviews.OrderByDescending(r => r.ReviewDate).FirstOrDefault())
                .Where(r => r != null)
                .ToListAsync();

            var weakCards = latestReviews.Count(r => r!.Rating == "Forgot" || r.Rating == "Hard");
            var upcomingCards = latestReviews.Count(r => r!.NextReviewDate >= tomorrowUtc && r.NextReviewDate <= nextWeekUtc);

            return new StudyStatsDto
            {
                DueToday = dueCardIds.Count,
                ReviewedToday = reviewedToday,
                TotalCards = await cards.CountAsync(),
                WeakCards = weakCards,
                UpcomingCards = upcomingCards,
                StreakDays = await CalculateStreakDaysAsync(userId),
                Decks = decks
            };
        }

        private async Task<HashSet<long>> GetDueCardIdsAsync(Guid userId, int? deckId = null)
        {
            var nowUtc = DateTime.UtcNow;
            var query = _context.StudyCards
                .Where(c => c.Deck.UserId == userId);

            if (deckId.HasValue)
            {
                query = query.Where(c => c.DeckId == deckId.Value);
            }

            var cards = await query
                .Select(c => new
                {
                    c.Id,
                    LatestReview = c.StudyReviews.OrderByDescending(r => r.ReviewDate).FirstOrDefault()
                })
                .ToListAsync();

            return cards
                .Where(c => c.LatestReview == null || c.LatestReview.NextReviewDate <= nowUtc)
                .Select(c => c.Id)
                .ToHashSet();
        }

        private static StudyCardDto ToCardDto(StudyCard card)
        {
            var latest = card.StudyReviews?.OrderByDescending(r => r.ReviewDate).FirstOrDefault();
            return new StudyCardDto
            {
                Id = card.Id,
                DeckId = card.DeckId,
                DeckName = card.Deck?.Name ?? string.Empty,
                CardType = card.CardType,
                FrontText = card.FrontText,
                BackText = card.BackText,
                Example = card.Example,
                Note = card.Note,
                Pronunciation = card.Pronunciation,
                Tags = card.Tags,
                ExtendedData = card.ExtendedData,
                NormalizedText = card.NormalizedText,
                Difficulty = card.Difficulty,
                CreatedAt = card.CreatedAt,
                UpdatedAt = card.UpdatedAt,
                LastReviewDate = latest?.ReviewDate,
                NextReviewDate = latest?.NextReviewDate,
                IntervalDays = latest?.IntervalDays ?? 0,
                ReviewCount = latest?.ReviewCount ?? 0,
                LastRating = latest?.Rating
            };
        }

        private static string NormalizeStudyText(string? value)
        {
            return (value ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string ResolveDeckLanguage(string? language, string? name, string? description)
        {
            var normalized = NormalizeDeckLanguage(language);
            if (!string.IsNullOrWhiteSpace(normalized) && normalized != "other")
            {
                return normalized;
            }

            var hint = $"{name} {description}".ToLowerInvariant();
            if (hint.Contains("english") || hint.Contains("tieng anh") || hint.Contains("tiếng anh") || hint.Contains("interview"))
            {
                return "english";
            }

            if (hint.Contains("japanese") || hint.Contains("japan") || hint.Contains("n5") || hint.Contains("nihongo") || hint.Contains("tieng nhat") || hint.Contains("tiếng nhật"))
            {
                return "japanese";
            }

            return "other";
        }

        private static string NormalizeDeckLanguage(string? language)
        {
            var value = language?.Trim().ToLowerInvariant() ?? "";
            return value switch
            {
                "english" or "en" or "eng" or "tieng anh" or "tiếng anh" => "english",
                "japanese" or "ja" or "jp" or "japan" or "nihongo" or "tieng nhat" or "tiếng nhật" => "japanese",
                "" => "",
                _ => value
            };
        }

        private static string NormalizeCardType(string value)
        {
            return value switch
            {
                "Vocabulary" or "Grammar" or "QA" => value,
                _ => "Vocabulary"
            };
        }

        private static string NormalizeRating(string value)
        {
            return value switch
            {
                "Forgot" or "Hard" or "Good" or "Easy" => value,
                _ => "Good"
            };
        }

        private static int CalculateIntervalDays(string rating, int oldInterval)
        {
            return rating switch
            {
                "Forgot" => 0,
                "Hard" => 0,
                "Good" => 0,
                "Easy" => oldInterval == 0 ? 5 : oldInterval * 3,
                _ => 0
            };
        }

        private static DateTime CalculateNextReviewDate(string rating, int intervalDays)
        {
            var now = DateTime.UtcNow;
            return rating switch
            {
                "Forgot" => now,
                "Hard" => now.AddMinutes(15),
                "Good" => now.AddHours(8),
                "Easy" => now.AddDays(intervalDays),
                _ => now.AddHours(8)
            };
        }

        private static decimal CalculateEaseFactor(string rating, decimal oldEaseFactor)
        {
            var next = rating switch
            {
                "Forgot" => oldEaseFactor - 0.2m,
                "Hard" => oldEaseFactor - 0.1m,
                "Easy" => oldEaseFactor + 0.15m,
                _ => oldEaseFactor
            };

            return Math.Max(1.3m, Math.Min(3.0m, next));
        }

        private async Task<int> CalculateStreakDaysAsync(Guid userId)
        {
            var reviewDates = await _context.StudyReviews
                .Where(r => r.Card.Deck.UserId == userId)
                .Select(r => r.ReviewDate.Date)
                .Distinct()
                .ToListAsync();

            var reviewSet = reviewDates.ToHashSet();
            var cursor = DateTime.UtcNow.Date;
            var streak = 0;

            while (reviewSet.Contains(cursor))
            {
                streak++;
                cursor = cursor.AddDays(-1);
            }

            return streak;
        }
    }
}
