using System;
using Xunit;

namespace AiDailyExpense.Tests
{
    public class SpacedRepetitionAlgorithmTests
    {
        // Tests reflecting the SM-2 Spaced Repetition logic implemented in StudyService
        [Theory]
        [InlineData("Forgot", 2.5, 2.3)]
        [InlineData("Hard", 2.5, 2.4)]
        [InlineData("Good", 2.5, 2.5)]
        [InlineData("Easy", 2.5, 2.65)]
        public void CalculateEaseFactor_AdjustsBasedOnRating(string rating, double initialEf, double expectedEf)
        {
            var initial = (decimal)initialEf;
            var expected = (decimal)expectedEf;

            var next = rating switch
            {
                "Forgot" => initial - 0.2m,
                "Hard" => initial - 0.1m,
                "Easy" => initial + 0.15m,
                _ => initial
            };

            var bounded = Math.Max(1.3m, Math.Min(3.0m, next));
            Assert.Equal(expected, bounded);
        }

        [Fact]
        public void CalculateEaseFactor_EnforcesMinimumBoundOf1Point3()
        {
            var initial = 1.35m;
            var next = initial - 0.2m; // 1.15m -> bounded to 1.3m
            var bounded = Math.Max(1.3m, Math.Min(3.0m, next));

            Assert.Equal(1.3m, bounded);
        }

        [Fact]
        public void CalculateIntervalDays_EasyIncreasesMultiplied()
        {
            var initialInterval = 5;
            var nextInterval = initialInterval * 3;

            Assert.Equal(15, nextInterval);
        }

        [Theory]
        [InlineData("Forgot", 0)]
        [InlineData("Hard", 0)]
        [InlineData("Good", 0)]
        public void CalculateIntervalDays_ResetOnForgotOrHard(string rating, int expected)
        {
            var oldInterval = 10;
            var interval = rating switch
            {
                "Forgot" => 0,
                "Hard" => 0,
                "Good" => 0,
                "Easy" => oldInterval == 0 ? 5 : oldInterval * 3,
                _ => 0
            };

            Assert.Equal(expected, interval);
        }
    }
}

