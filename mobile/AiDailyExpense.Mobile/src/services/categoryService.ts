import api from './api';
import { ApiResponse } from '../types/api';
import { Category } from '../types/category';

const categoryService = {
  getCategories: async (): Promise<ApiResponse<Category[]>> => {
    const response = await api.get<ApiResponse<Category[]>>('/categories');
    return response.data;
  },

  createCategory: async (payload: { name: string; type: string; icon?: string; color?: string }): Promise<ApiResponse<Category>> => {
    const response = await api.post<ApiResponse<Category>>('/categories', payload);
    return response.data;
  },
};

export default categoryService;
