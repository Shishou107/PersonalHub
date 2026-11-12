using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace AiDailyExpense.Api.Helpers
{
    public static class MoneyParser
    {
        private sealed class MoneyCandidate
        {
            public decimal Amount { get; set; }
            public int Score { get; set; }
            public int Index { get; set; }
        }

        public static decimal Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;

            var normalizedText = text
                .Replace('\u00A0', ' ')
                .Replace('−', '-')
                .Replace('–', '-')
                .Replace('—', '-');

            var candidates = new List<MoneyCandidate>();
            var pattern = @"(?<![\p{L}\d])(?<sign>[+-])?\s*(?<num>\d{1,3}(?:[.,]\d{3})+|\d+(?:[.,]\d+)?)(?:\s*(?<unit>k|nghìn|ngàn|ngan|tr|triệu|trieu|m|vnd|vnđ|đ|d))?\b";

            foreach (Match match in Regex.Matches(normalizedText, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (!match.Success) continue;
                if (LooksLikeTime(normalizedText, match.Index, match.Index + match.Length)) continue;
                if (LooksLikeDatePart(normalizedText, match.Index, match.Index + match.Length)) continue;

                var numStr = match.Groups["num"].Value;
                var unitStr = match.Groups["unit"].Value;
                var before = normalizedText.Substring(Math.Max(0, match.Index - 25), Math.Min(25, match.Index));
                var afterStart = Math.Min(normalizedText.Length, match.Index + match.Length);
                var after = normalizedText.Substring(afterStart, Math.Min(25, normalizedText.Length - afterStart));

                if (IsNonMoneyNumber(numStr, unitStr, before, after)) continue;

                var amount = ParseNumber(numStr, unitStr);
                if (amount <= 0) continue;

                var score = 0;
                var sign = match.Groups["sign"].Value;
                var unit = unitStr.ToLowerInvariant();
                var context = $"{before} {after}".ToLowerInvariant();

                if (sign == "+" || sign == "-") score += 120;
                if (IsCurrencyUnit(unit)) score += 90;
                if (IsMultiplierUnit(unit)) score += 80;
                if (Regex.IsMatch(context, @"\b(vnd|vnđ|so tien|số tiền|gd|ps|phat sinh|phát sinh|thanh toan|thanh toán|chuyen|chuyển)\b", RegexOptions.IgnoreCase)) score += 45;
                if (amount >= 10000) score += 25;
                if (amount >= 100000) score += 20;
                if (amount >= 1000000) score += 15;
                if (Regex.IsMatch(before, @"(?:\btk\b|\bstk\b|tai khoan|tài khoản|account)\s*$", RegexOptions.IgnoreCase)) score -= 90;
                if (Regex.IsMatch(before, @"[xX]\s*$")) score -= 90;

                candidates.Add(new MoneyCandidate
                {
                    Amount = amount,
                    Score = score,
                    Index = match.Index
                });
            }

            return candidates
                .OrderByDescending(c => c.Score)
                .ThenByDescending(c => c.Amount)
                .ThenBy(c => c.Index)
                .FirstOrDefault()?.Amount ?? 0;
        }

        private static bool IsNonMoneyNumber(string numStr, string unitStr, string before, string after)
        {
            var cleanedNum = new string(numStr.Where(char.IsDigit).ToArray());
            var beforeLower = before.ToLowerInvariant();
            var afterLower = after.ToLowerInvariant();
            var unitLower = unitStr.Trim().ToLowerInvariant();

            // A number with a multiplier unit (k, triệu, tr, tỷ, m) is always money
            var hasMultiplier = IsMultiplierUnit(unitLower);
            if (hasMultiplier) return false;

            // 1. Check leading zero numbers (often phone numbers or account numbers)
            // e.g. 0869253074, 0909123456
            if (cleanedNum.StartsWith("0") && cleanedNum.Length >= 2)
            {
                // Exceptions: decimals starting with 0 (e.g. 0.5, 0,25) -> but we already checked hasMultiplier is false.
                return true;
            }

            // 2. Preceding text keyword checks
            // We only check these if the number doesn't have a currency unit (like đ, vnd)
            // because a money amount like "100.000đ" is never a phone/account number.
            var hasCurrency = IsCurrencyUnit(unitLower);
            if (!hasCurrency)
            {
                // Check if the preceding word is a phone number label and the number looks like a phone number
                // (starts with 0, 8, 9, or has length >= 9)
                var phonePattern = @"\b(sđt|sdt|đt|dt|phone|điện thoại|dien thoai|thuê bao|thue bao|cho số|cho so|đến số|den so|gửi đến|gui den|nạp cho|nap cho|sđt nhận|sdt nhan)\b\s*:?\s*$";
                if (Regex.IsMatch(beforeLower, phonePattern))
                {
                    if (cleanedNum.Length >= 9 || cleanedNum.StartsWith("0") || cleanedNum.StartsWith("8") || cleanedNum.StartsWith("9"))
                    {
                        return true;
                    }
                }

                // Check if the preceding word is an account/card number label
                var accountPattern = @"\b(tk|stk|stk nguồn|stk nguon|stk nhận|stk nhan|tài khoản|tai khoan|tai khoan nguon|tài khoản nguồn|tài khoản nhận|tai khoan nhan|account|thẻ nguồn|the nguon|số thẻ|so the|thẻ|the)\b\s*:?\s*$";
                if (Regex.IsMatch(beforeLower, accountPattern))
                {
                    // Account numbers are usually long (>= 4 digits)
                    if (cleanedNum.Length >= 4)
                    {
                        return true;
                    }
                }

                // Check if it's an OTP code
                var otpPattern = @"\b(otp|mã|ma|mã xác thực|ma xac thuc|mã otp|ma otp|mã xác nhận|ma xac nhan|mã giao dịch|ma giao dich|magd|ma gd)\b\s*:?\s*$";
                if (Regex.IsMatch(beforeLower, otpPattern))
                {
                    if (cleanedNum.Length >= 4 && cleanedNum.Length <= 8)
                    {
                        return true;
                    }
                }

                // General context check for OTP / phone number in the same clause
                if (beforeLower.Contains("số điện thoại") || beforeLower.Contains("so dien thoai") ||
                    beforeLower.Contains("thuê bao") || beforeLower.Contains("thue bao"))
                {
                    if (!beforeLower.Contains(".") && !beforeLower.Contains(";"))
                    {
                        if (cleanedNum.Length >= 9)
                        {
                            return true;
                        }
                    }
                }

                if (beforeLower.Contains("otp") || beforeLower.Contains("mã xác") || beforeLower.Contains("ma xac") ||
                    beforeLower.Contains("mã gd") || beforeLower.Contains("ma gd"))
                {
                    if (!beforeLower.Contains(".") && !beforeLower.Contains(";"))
                    {
                        if (cleanedNum.Length >= 4 && cleanedNum.Length <= 8)
                        {
                            return true;
                        }
                    }
                }
            }

            // 3. Large arbitrary numbers without separators that are likely account numbers or serial numbers
            // e.g. 10 or more digits with no thousand separators (like 123456789012)
            if (cleanedNum.Length >= 10 && !numStr.Contains(".") && !numStr.Contains(","))
            {
                return true;
            }

            return false;
        }

        private static decimal ParseNumber(string rawNumber, string unit)
        {
            var number = rawNumber.Trim();
            var normalizedUnit = unit.Trim().ToLowerInvariant();
            var multiplier = GetMultiplier(normalizedUnit);
            var value = IsMultiplierUnit(normalizedUnit)
                ? ParseDecimalNumber(number)
                : ParseVndIntegerNumber(number);

            return Math.Round(value * multiplier, 0, MidpointRounding.AwayFromZero);
        }

        private static decimal ParseVndIntegerNumber(string number)
        {
            var separatorMatches = Regex.Matches(number, @"[.,]");
            if (separatorMatches.Count == 0)
            {
                return decimal.TryParse(number, NumberStyles.Number, CultureInfo.InvariantCulture, out var direct)
                    ? direct
                    : 0;
            }

            var parts = Regex.Split(number, @"[.,]");
            var allThousandGroups = parts.Length > 1
                && parts.Skip(1).All(part => part.Length == 3)
                && parts[0].Length >= 1
                && parts[0].Length <= 3;

            if (allThousandGroups)
            {
                var cleaned = Regex.Replace(number, @"[.,]", "");
                return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var grouped)
                    ? grouped
                    : 0;
            }

            return ParseDecimalNumber(number);
        }

        private static decimal ParseDecimalNumber(string number)
        {
            var normalized = number.Trim();
            var lastDot = normalized.LastIndexOf('.');
            var lastComma = normalized.LastIndexOf(',');

            if (lastDot >= 0 && lastComma >= 0)
            {
                var decimalSeparator = lastDot > lastComma ? "." : ",";
                var groupSeparator = decimalSeparator == "." ? "," : ".";
                normalized = normalized.Replace(groupSeparator, "");
                normalized = normalized.Replace(decimalSeparator, ".");
            }
            else
            {
                normalized = normalized.Replace(',', '.');
            }

            return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
                ? value
                : 0;
        }

        private static decimal GetMultiplier(string unit)
        {
            if (unit is "k" or "nghìn" or "ngàn" or "ngan") return 1000;
            if (unit is "tr" or "triệu" or "trieu" or "m") return 1000000;
            return 1;
        }

        private static bool IsMultiplierUnit(string unit)
        {
            return unit is "k" or "nghìn" or "ngàn" or "ngan" or "tr" or "triệu" or "trieu" or "m";
        }

        private static bool IsCurrencyUnit(string unit)
        {
            return unit is "vnd" or "vnđ" or "đ" or "d";
        }

        private static bool LooksLikeTime(string text, int start, int end)
        {
            var charBefore = start > 0 ? text[start - 1] : '\0';
            var charAfter = end < text.Length ? text[end] : '\0';
            return charBefore == ':' || charAfter == ':';
        }

        private static bool LooksLikeDatePart(string text, int start, int end)
        {
            var charBefore = start > 0 ? text[start - 1] : '\0';
            var charAfter = end < text.Length ? text[end] : '\0';
            return charBefore == '/' || charBefore == '-' || charAfter == '/' || charAfter == '-';
        }
    }
}
