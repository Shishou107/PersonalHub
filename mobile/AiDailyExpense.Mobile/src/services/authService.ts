import api from './api';
import { ApiResponse } from '../types/api';
import { AuthData, User } from '../types/auth';

export interface LoginPayload {
  email: string;
  password?: string;
}

export interface RegisterPayload {
  fullName: string;
  email: string;
  password?: string;
  confirmPassword?: string;
}

export interface InitializeBalancePayload {
  cashBalance: number;
  bankBalance: number;
}

export interface ResetPasswordPayload {
  email: string;
  code: string;
  newPassword?: string;
  confirmNewPassword?: string;
}

const authService = {
  login: async (payload: LoginPayload): Promise<ApiResponse<AuthData>> => {
    const response = await api.post<ApiResponse<AuthData>>('/auth/login', payload);
    return response.data;
  },

  register: async (payload: RegisterPayload): Promise<ApiResponse<AuthData>> => {
    const response = await api.post<ApiResponse<AuthData>>('/auth/register', payload);
    return response.data;
  },

  me: async (): Promise<ApiResponse<User>> => {
    const response = await api.get<ApiResponse<User>>('/auth/me');
    return response.data;
  },

  initializeBalance: async (payload: InitializeBalancePayload): Promise<ApiResponse<User>> => {
    const response = await api.post<ApiResponse<User>>('/auth/initialize-balance', payload);
    return response.data;
  },

  requestEmailVerification: async (email: string): Promise<ApiResponse<boolean>> => {
    const response = await api.post<ApiResponse<boolean>>('/auth/request-email-verification', { email });
    return response.data;
  },

  verifyEmail: async (email: string, code: string): Promise<ApiResponse<boolean>> => {
    const response = await api.post<ApiResponse<boolean>>('/auth/verify-email', { email, code });
    return response.data;
  },

  forgotPassword: async (email: string): Promise<ApiResponse<boolean>> => {
    const response = await api.post<ApiResponse<boolean>>('/auth/forgot-password', { email });
    return response.data;
  },

  resetPassword: async (payload: ResetPasswordPayload): Promise<ApiResponse<boolean>> => {
    const response = await api.post<ApiResponse<boolean>>('/auth/reset-password', payload);
    return response.data;
  },
};

export default authService;
