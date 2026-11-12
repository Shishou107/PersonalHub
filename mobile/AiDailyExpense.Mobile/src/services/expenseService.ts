import api from './api';
import { ApiResponse } from '../types/api';
import { Expense, CreateExpensePayload, UpdateExpensePayload } from '../types/expense';

const expenseService = {
  getExpenses: async (fromDate?: string, toDate?: string): Promise<ApiResponse<Expense[]>> => {
    let url = '/expenses';
    const params = new URLSearchParams();
    if (fromDate) params.append('fromDate', fromDate);
    if (toDate) params.append('toDate', toDate);
    const queryString = params.toString();
    if (queryString) {
      url += `?${queryString}`;
    }
    const response = await api.get<ApiResponse<Expense[]>>(url);
    return response.data;
  },

  getExpenseById: async (id: number): Promise<ApiResponse<Expense>> => {
    const response = await api.get<ApiResponse<Expense>>(`/expenses/${id}`);
    return response.data;
  },

  createExpense: async (payload: CreateExpensePayload): Promise<ApiResponse<Expense>> => {
    const response = await api.post<ApiResponse<Expense>>('/expenses', payload);
    return response.data;
  },

  updateExpense: async (id: number, payload: UpdateExpensePayload): Promise<ApiResponse<Expense>> => {
    const response = await api.put<ApiResponse<Expense>>(`/expenses/${id}`, payload);
    return response.data;
  },

  deleteExpense: async (id: number): Promise<ApiResponse<boolean>> => {
    const response = await api.delete<ApiResponse<boolean>>(`/expenses/${id}`);
    return response.data;
  },
};

export default expenseService;
