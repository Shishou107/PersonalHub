import React, { createContext, useState, useEffect, useContext } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import authService from '../services/authService';
import { User } from '../types/auth';
import { STORAGE_TOKEN_KEY, STORAGE_USER_KEY } from '../utils/constants';

interface AuthContextType {
  isAuthenticated: boolean;
  user: User | null;
  token: string | null;
  isLoading: boolean;
  login: (email: string, password?: string) => Promise<void>;
  register: (fullName: string, email: string, password?: string, confirmPassword?: string) => Promise<void>;
  initializeBalance: (cashBalance: number, bankBalance: number) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<User | null>(null);
  const [token, setToken] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    loadStorageData();
  }, []);

  const loadStorageData = async () => {
    try {
      const storedToken = await AsyncStorage.getItem(STORAGE_TOKEN_KEY);
      const storedUser = await AsyncStorage.getItem(STORAGE_USER_KEY);

      if (storedToken && storedUser) {
        setToken(storedToken);
        setUser(JSON.parse(storedUser));

        // Option: Verify session validity via API
        try {
          const res = await authService.me();
          if (res.success && res.data) {
            setUser(res.data);
            await AsyncStorage.setItem(STORAGE_USER_KEY, JSON.stringify(res.data));
          } else {
            await logout();
          }
        } catch (err: any) {
          if (err.response?.status === 401) {
            await logout();
            return;
          }
          // If network is down, we keep offline token/user info
          console.warn('Không thể kiểm tra phiên đăng nhập hiện tại từ server, giữ thông tin offline:', err);
        }
      }
    } catch (e) {
      console.error('Lỗi khi tải thông tin lưu trữ đăng nhập:', e);
    } finally {
      setIsLoading(false);
    }
  };

  const login = async (email: string, password?: string) => {
    setIsLoading(true);
    try {
      const res = await authService.login({ email, password });
      if (res.success && res.data) {
        const { token: jwtToken, user: userData } = res.data;
        setToken(jwtToken);
        setUser(userData);
        await AsyncStorage.setItem(STORAGE_TOKEN_KEY, jwtToken);
        await AsyncStorage.setItem(STORAGE_USER_KEY, JSON.stringify(userData));
      } else {
        throw new Error(res.message || 'Đăng nhập không thành công.');
      }
    } catch (error: any) {
      setIsLoading(false);
      console.error('Lỗi đăng nhập chi tiết:', error);
      const errorMsg = error.response?.data?.message || error.message || 'Đăng nhập thất bại.';
      throw new Error(errorMsg);
    } finally {
      setIsLoading(false);
    }
  };

  const register = async (fullName: string, email: string, password?: string, confirmPassword?: string) => {
    setIsLoading(true);
    try {
      const res = await authService.register({ fullName, email, password, confirmPassword });
      if (res.success && res.data) {
        const { token: jwtToken, user: userData } = res.data;
        setToken(jwtToken);
        setUser(userData);
        await AsyncStorage.setItem(STORAGE_TOKEN_KEY, jwtToken);
        await AsyncStorage.setItem(STORAGE_USER_KEY, JSON.stringify(userData));
      } else {
        throw new Error(res.message || 'Đăng ký không thành công.');
      }
    } catch (error: any) {
      setIsLoading(false);
      console.error('Lỗi đăng ký chi tiết:', error);
      const errorMsg = error.response?.data?.message || error.message || 'Đăng ký thất bại.';
      throw new Error(errorMsg);
    } finally {
      setIsLoading(false);
    }
  };

  const initializeBalance = async (cashBalance: number, bankBalance: number) => {
    const res = await authService.initializeBalance({ cashBalance, bankBalance });
    if (res.success && res.data) {
      setUser(res.data);
      await AsyncStorage.setItem(STORAGE_USER_KEY, JSON.stringify(res.data));
    } else {
      throw new Error(res.message || 'Không thể thiết lập số dư ban đầu.');
    }
  };

  const refreshUser = async () => {
    try {
      const res = await authService.me();
      if (res.success && res.data) {
        setUser(res.data);
        await AsyncStorage.setItem(STORAGE_USER_KEY, JSON.stringify(res.data));
      }
    } catch (err) {
      console.error('Lỗi khi tải lại thông tin người dùng:', err);
    }
  };

  const logout = async () => {
    setIsLoading(true);
    try {
      setToken(null);
      setUser(null);
      await AsyncStorage.removeItem(STORAGE_TOKEN_KEY);
      await AsyncStorage.removeItem(STORAGE_USER_KEY);
    } catch (e) {
      console.error('Lỗi khi đăng xuất:', e);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <AuthContext.Provider
      value={{
        isAuthenticated: !!token,
        user,
        token,
        isLoading,
        login,
        register,
        initializeBalance,
        logout,
        refreshUser,
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (context === undefined) {
    throw new Error('useAuth phải được dùng bên trong AuthProvider');
  }
  return context;
};
