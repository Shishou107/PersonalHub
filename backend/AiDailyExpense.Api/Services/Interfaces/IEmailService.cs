using System.Threading.Tasks;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendCodeAsync(string toEmail, string subject, string code);
    }
}
