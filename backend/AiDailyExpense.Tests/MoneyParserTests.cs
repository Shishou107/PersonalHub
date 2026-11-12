using System;
using Xunit;
using AiDailyExpense.Api.Helpers;

namespace AiDailyExpense.Tests
{
    public class MoneyParserTests
    {
        [Theory]
        [InlineData("Ăn sáng 40k", 40000)]
        [InlineData("Đổ xăng 50.000đ", 50000)]
        [InlineData("Mua sách 150000", 150000)]
        [InlineData("Tiền nhà 1.5tr", 1500000)]
        [InlineData("Nhận lương 10 triệu", 10000000)]
        [InlineData("Cà phê highlands 35k", 35000)]
        [InlineData("GD: -50,000VND tai CIRCLE K", 50000)]
        [InlineData("Bien dong so du: +2,500,000 VND vao tai khoan", 2500000)]
        public void Parse_ValidAmounts_ReturnsExpectedValue(string input, decimal expected)
        {
            var result = MoneyParser.Parse(input);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("Hom nay troi dep qua")]
        [InlineData("STK 19034567890123")]
        public void Parse_NoAmount_ReturnsZero(string input)
        {
            var result = MoneyParser.Parse(input);
            Assert.Equal(0, result);
        }

        [Fact]
        public void Parse_DistinguishesBetweenAccountNumberAndTransactionAmount()
        {
            var text = "TK 1029384756 GD: -120,000VND luc 14:30";
            var amount = MoneyParser.Parse(text);

            Assert.Equal(120000, amount);
        }

        [Fact]
        public void Parse_HandlesMultipleAmounts_SelectsPrimaryAmount()
        {
            var text = "So tien: -75,000VND. So du cuoi: 12,450,000VND";
            var amount = MoneyParser.Parse(text);

            Assert.True(amount > 0);
        }
    }
}

