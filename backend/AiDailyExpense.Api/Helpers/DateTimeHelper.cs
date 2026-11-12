using System;

namespace AiDailyExpense.Api.Helpers
{
    public static class DateTimeHelper
    {
        public static TimeZoneInfo GetUserTimeZone(string? timeZoneId)
        {
            if (string.IsNullOrEmpty(timeZoneId))
            {
                timeZoneId = "Asia/Ho_Chi_Minh";
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch
            {
                // Fallback mapping for IANA timezone names to Windows Standard names
                if (timeZoneId == "Asia/Ho_Chi_Minh" || timeZoneId == "Asia/Saigon")
                {
                    try
                    {
                        return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                    }
                    catch { }
                }
                
                return TimeZoneInfo.Utc;
            }
        }

        public static DateTime ConvertToUserLocal(DateTime utcTime, string? timeZoneId)
        {
            var tz = GetUserTimeZone(timeZoneId);
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
        }

        public static DateTime ConvertToUtc(DateTime localTime, string? timeZoneId)
        {
            var tz = GetUserTimeZone(timeZoneId);
            var unspecified = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
            return TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        }

        public static (DateTime StartUtc, DateTime EndUtc) GetUtcRangeForLocalDate(DateTime localDate, string? timeZoneId)
        {
            var tz = GetUserTimeZone(timeZoneId);
            var startLocal = localDate.Date;
            var endLocal = startLocal.AddDays(1);

            var startUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startLocal, DateTimeKind.Unspecified), tz);
            var endUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endLocal, DateTimeKind.Unspecified), tz);

            return (startUtc, endUtc);
        }
    }
}
