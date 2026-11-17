using System;

namespace AiDailyExpense.Api.Helpers
{
    public record SpacedRepetitionResult(
        int IntervalDays,
        decimal EaseFactor,
        DateTime NextReviewDate,
        string Difficulty
    );

    /// <summary>
    /// Pure algorithmic implementation of the customized SuperMemo SM-2 Spaced Repetition System.
    /// Supports micro-learning intraday checkpoints (Good: 12h, Hard: 15m) and optimized 7-step Easy scaling to 365 days.
    /// </summary>
    public static class SpacedRepetitionAlgorithm
    {
        public const decimal DefaultEaseFactor = 2.5m;
        public const decimal MinEaseFactor = 1.3m;
        public const decimal MaxEaseFactor = 3.0m;

        public static string NormalizeRating(string? value)
        {
            return value?.Trim() switch
            {
                "Forgot" => "Forgot",
                "Hard" => "Hard",
                "Good" => "Good",
                "Easy" => "Easy",
                _ => "Good"
            };
        }

        public static SpacedRepetitionResult CalculateNextReview(
            string rawRating,
            int currentIntervalDays,
            decimal currentEaseFactor,
            DateTime? baseTimeUtc = null)
        {
            var rating = NormalizeRating(rawRating);
            var now = baseTimeUtc ?? DateTime.UtcNow;
            var ef = Math.Max(MinEaseFactor, Math.Min(MaxEaseFactor, currentEaseFactor <= 0 ? DefaultEaseFactor : currentEaseFactor));

            switch (rating)
            {
                case "Forgot":
                    {
                        var nextEf = Math.Max(MinEaseFactor, ef - 0.2m);
                        return new SpacedRepetitionResult(
                            IntervalDays: 0,
                            EaseFactor: nextEf,
                            NextReviewDate: now,
                            Difficulty: "Weak"
                        );
                    }

                case "Hard":
                    {
                        var nextEf = Math.Max(MinEaseFactor, ef - 0.15m);
                        // Review again in 15 minutes to consolidate short-term memory
                        return new SpacedRepetitionResult(
                            IntervalDays: 0,
                            EaseFactor: nextEf,
                            NextReviewDate: now.AddMinutes(15),
                            Difficulty: "Weak"
                        );
                    }

                case "Good":
                    {
                        // If card is brand new or previously forgot/hard, first Good checkpoint is 12 hours (nửa ngày)
                        if (currentIntervalDays <= 0)
                        {
                            return new SpacedRepetitionResult(
                                IntervalDays: 0,
                                EaseFactor: ef,
                                NextReviewDate: now.AddHours(12),
                                Difficulty: "Good"
                            );
                        }

                        // If card already passed the 12h mark: scale interval smoothly without resetting
                        int nextInterval;
                        if (currentIntervalDays == 1)
                        {
                            nextInterval = 3;
                        }
                        else
                        {
                            nextInterval = Math.Max(currentIntervalDays + 1, (int)Math.Round(currentIntervalDays * 1.5m));
                        }

                        return new SpacedRepetitionResult(
                            IntervalDays: nextInterval,
                            EaseFactor: ef,
                            NextReviewDate: now.AddDays(nextInterval),
                            Difficulty: nextInterval >= 365 ? "Mastered" : "Good"
                        );
                    }

                case "Easy":
                    {
                        var nextEf = Math.Min(MaxEaseFactor, ef + 0.15m);

                        // 7-step optimized expansion progression to 365 days
                        int nextInterval;
                        if (currentIntervalDays <= 0)
                        {
                            nextInterval = 3;   // Step 1: 3 days
                        }
                        else if (currentIntervalDays < 7)
                        {
                            nextInterval = 7;   // Step 2: 7 days (1 week)
                        }
                        else if (currentIntervalDays < 16)
                        {
                            nextInterval = 16;  // Step 3: 16 days (~2 weeks)
                        }
                        else if (currentIntervalDays < 35)
                        {
                            nextInterval = 35;  // Step 4: 35 days (~1 month)
                        }
                        else if (currentIntervalDays < 80)
                        {
                            nextInterval = 80;  // Step 5: 80 days (~2.5 months)
                        }
                        else if (currentIntervalDays < 180)
                        {
                            nextInterval = 180; // Step 6: 180 days (~6 months)
                        }
                        else
                        {
                            nextInterval = 365; // Step 7: 365 days (1 year - Mastered)
                        }

                        return new SpacedRepetitionResult(
                            IntervalDays: nextInterval,
                            EaseFactor: nextEf,
                            NextReviewDate: now.AddDays(nextInterval),
                            Difficulty: nextInterval >= 365 ? "Mastered" : "Easy"
                        );
                    }

                default:
                    {
                        return new SpacedRepetitionResult(
                            IntervalDays: Math.Max(1, currentIntervalDays),
                            EaseFactor: ef,
                            NextReviewDate: now.AddDays(Math.Max(1, currentIntervalDays)),
                            Difficulty: "Good"
                        );
                    }
            }
        }
    }
}
