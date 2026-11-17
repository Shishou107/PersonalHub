// Base API URL configuration
// 1. Prioritize EXPO_PUBLIC_API_URL defined in environment variables or .env/.env.local
// 2. Default to standard HTTPS endpoint
const DEFAULT_API_URL = 'https://testapi.somee.com/api';

export const API_BASE_URL = process.env.EXPO_PUBLIC_API_URL || DEFAULT_API_URL;
export const STORAGE_TOKEN_KEY = 'ai_daily_expense_token';
export const STORAGE_USER_KEY = '@ai_daily_expense_user';
