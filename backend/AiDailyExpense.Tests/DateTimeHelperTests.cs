using System;
using Xunit;
using AiDailyExpense.Api.Helpers;

namespace AiDailyExpense.Tests
{
    public class DateTimeHelperTests
    {
        [Fact]
        public void GetUserTimeZone_VietnamTimezone_ReturnsValidTimeZoneInfo()
        {
            var tz = DateTimeHelper.GetUserTimeZone("Asia/Ho_Chi_Minh");

            Assert.NotNull(tz);
            Assert.True(tz.BaseUtcOffset.TotalHours >= 7.0);
        }

        [Fact]
        public void GetUserTimeZone_NullOrEmpty_DefaultsToVietnam()
        {
            var tz = DateTimeHelper.GetUserTimeZone(null);

            Assert.NotNull(tz);
            Assert.True(tz.BaseUtcOffset.TotalHours >= 7.0);
        }

        [Fact]
        public void ConvertToUtc_And_ConvertToUserLocal_AreReversible()
        {
            var timeZoneId = "Asia/Ho_Chi_Minh";
            var localNow = new DateTime(2026, 5, 20, 15, 30, 0);

            var utcTime = DateTimeHelper.ConvertToUtc(localNow, timeZoneId);
            var backToLocal = DateTimeHelper.ConvertToUserLocal(utcTime, timeZoneId);

            Assert.Equal(localNow, backToLocal);
        }

        [Fact]
        public void GetUtcRangeForLocalDate_CoversFullDay24Hours()
        {
            var timeZoneId = "Asia/Ho_Chi_Minh";
            var localDate = new DateTime(2026, 6, 1);

            var range = DateTimeHelper.GetUtcRangeForLocalDate(localDate, timeZoneId);

            var duration = range.EndUtc - range.StartUtc;
            Assert.Equal(TimeSpan.FromDays(1), duration);
        }
    }
}

