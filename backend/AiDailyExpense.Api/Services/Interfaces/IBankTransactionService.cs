using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AiDailyExpense.Api.DTOs.BankTransactions;
using AiDailyExpense.Api.DTOs.Expenses;

namespace AiDailyExpense.Api.Services.Interfaces
{
    public interface IBankTransactionService
    {
        Task<List<BankTransactionDto>> GetBankTransactionsAsync(Guid userId, DateTime? fromDate = null, DateTime? toDate = null);
        Task<BankTransactionDto> ProcessNotificationAsync(Guid userId, BankNotificationRequest request);
        Task<ExpenseDto> ConfirmTransactionAsync(Guid userId, long bankTransactionId, ConfirmBankTransactionRequest request);
        Task<bool> DeleteTransactionAsync(Guid userId, long bankTransactionId);
    }
}
