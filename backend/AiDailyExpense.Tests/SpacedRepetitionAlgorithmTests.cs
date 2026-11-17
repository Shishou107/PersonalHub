using System;
using AiDailyExpense.Api.Helpers;
using Xunit;

namespace AiDailyExpense.Tests
{
    public class SpacedRepetitionAlgorithmTests
    {
        private readonly DateTime _baseTime = new(2026, 11, 17, 10, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData("Forgot", "Forgot")]
        [InlineData("Hard", "Hard")]
        [InlineData("Good", "Good")]
        [InlineData("Easy", "Easy")]
        [InlineData("invalid", "Good")]
        [InlineData(null, "Good")]
        public void NormalizeRating_StandardizesRatings(string? input, string expected)
        {
            var actual = SpacedRepetitionAlgorithm.NormalizeRating(input);
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void CalculateNextReview_Forgot_ResetsIntervalAndDecreasesEaseFactor()
        {
            var result = SpacedRepetitionAlgorithm.CalculateNextReview("Forgot", currentIntervalDays: 45, currentEaseFactor: 2.5m, _baseTime);

            Assert.Equal(0, result.IntervalDays);
            Assert.Equal(2.3m, result.EaseFactor);
            Assert.Equal(_baseTime, result.NextReviewDate);
            Assert.Equal("Weak", result.Difficulty);
        }

        [Fact]
        public void CalculateNextReview_Hard_Schedules15MinutesConsolidation()
        {
            var result = SpacedRepetitionAlgorithm.CalculateNextReview("Hard", currentIntervalDays: 16, currentEaseFactor: 2.5m, _baseTime);

            Assert.Equal(0, result.IntervalDays);
            Assert.Equal(2.35m, result.EaseFactor);
            Assert.Equal(_baseTime.AddMinutes(15), result.NextReviewDate);
            Assert.Equal("Weak", result.Difficulty);
        }

        [Fact]
        public void CalculateNextReview_Good_InitialReview_Schedules12HoursCheckpoint()
        {
            // Initial review for brand new cards or cards coming back from Forgot/Hard
            var result = SpacedRepetitionAlgorithm.CalculateNextReview("Good", currentIntervalDays: 0, currentEaseFactor: 2.5m, _baseTime);

            Assert.Equal(0, result.IntervalDays);
            Assert.Equal(2.5m, result.EaseFactor);
            Assert.Equal(_baseTime.AddHours(12), result.NextReviewDate);
            Assert.Equal("Good", result.Difficulty);
        }

        [Fact]
        public void CalculateNextReview_Good_EstablishedCard_DoesNotResetToZero()
        {
            // A card at 10 days reviewed as Good must NOT reset to 0; it should progress smoothly
            var result = SpacedRepetitionAlgorithm.CalculateNextReview("Good", currentIntervalDays: 10, currentEaseFactor: 2.5m, _baseTime);

            Assert.Equal(15, result.IntervalDays);
            Assert.Equal(2.5m, result.EaseFactor);
            Assert.Equal(_baseTime.AddDays(15), result.NextReviewDate);
            Assert.Equal("Good", result.Difficulty);
        }

        [Theory]
        [InlineData(0, 3)]     // Step 1: 3 days
        [InlineData(3, 7)]     // Step 2: 7 days (1 week)
        [InlineData(7, 16)]    // Step 3: 16 days (~2 weeks)
        [InlineData(16, 35)]   // Step 4: 35 days (~1 month)
        [InlineData(35, 80)]   // Step 5: 80 days (~2.5 months)
        [InlineData(80, 180)]  // Step 6: 180 days (~6 months)
        [InlineData(180, 365)] // Step 7: 365 days (1 year - Mastered)
        [InlineData(365, 365)] // Post-mastery stays at 1 year
        public void CalculateNextReview_Easy_FollowsSevenStepProgressionTo365Days(int currentInterval, int expectedInterval)
        {
            var result = SpacedRepetitionAlgorithm.CalculateNextReview("Easy", currentInterval, currentEaseFactor: 2.5m, _baseTime);

            Assert.Equal(expectedInterval, result.IntervalDays);
            Assert.Equal(2.65m, result.EaseFactor);
            Assert.Equal(_baseTime.AddDays(expectedInterval), result.NextReviewDate);

            if (expectedInterval >= 365)
            {
                Assert.Equal("Mastered", result.Difficulty);
            }
            else
            {
                Assert.Equal("Easy", result.Difficulty);
            }
        }

        [Fact]
        public void CalculateNextReview_EnforcesMinimumEaseFactorBoundOf1Point3()
        {
            var result = SpacedRepetitionAlgorithm.CalculateNextReview("Forgot", currentIntervalDays: 5, currentEaseFactor: 1.35m, _baseTime);
            Assert.Equal(1.3m, result.EaseFactor);
        }

        [Fact]
        public void CalculateNextReview_EnforcesMaximumEaseFactorBoundOf3Point0()
        {
            var result = SpacedRepetitionAlgorithm.CalculateNextReview("Easy", currentIntervalDays: 5, currentEaseFactor: 2.95m, _baseTime);
            Assert.Equal(3.0m, result.EaseFactor);
        }
    }
}
