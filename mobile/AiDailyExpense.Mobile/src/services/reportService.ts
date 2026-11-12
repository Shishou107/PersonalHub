import api from './api';
import { ApiResponse } from '../types/api';
import { DashboardData, MonthlyExpenseReport } from '../types/report';

const reportService = {
  getDashboard: async (): Promise<ApiResponse<DashboardData>> => {
    const response = await api.get<ApiResponse<DashboardData>>('/reports/dashboard');
    return response.data;
  },

  getMonthlyReport: async (month: number, year: number): Promise<ApiResponse<MonthlyExpenseReport>> => {
    const response = await api.get<ApiResponse<MonthlyExpenseReport>>(`/reports/monthly-expense?month=${month}&year=${year}`);
    return response.data;
  },
};

export default reportService;
