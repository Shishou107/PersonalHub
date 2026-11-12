import api from './api';
import { ApiResponse } from '../types/api';
import { BankTransaction, ConfirmBankTransactionPayload } from '../types/bankTransaction';
import { Expense } from '../types/expense';

const bankTransactionService = {
  getTransactions: async (fromDate?: string, toDate?: string): Promise<ApiResponse<BankTransaction[]>> => {
    const response = await api.get<ApiResponse<BankTransaction[]>>('/bank-transactions', {
      params: { fromDate, toDate }
    });
    return response.data;
  },

  confirmTransaction: async (id: number, payload: ConfirmBankTransactionPayload): Promise<ApiResponse<Expense>> => {
    const response = await api.post<ApiResponse<Expense>>(`/bank-transactions/${id}/confirm`, payload);
    return response.data;
  },

  deleteTransaction: async (id: number): Promise<ApiResponse<boolean>> => {
    const response = await api.delete<ApiResponse<boolean>>(`/bank-transactions/${id}`);
    return response.data;
  },
};

export default bankTransactionService;
