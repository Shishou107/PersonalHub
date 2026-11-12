import api from './api';
import { ApiResponse } from '../types/api';
import {
  CreateVaultItemPayload,
  RevealedVaultItem,
  RevealVaultItemPayload,
  SetVaultPinPayload,
  VaultCategory,
  VaultItem,
} from '../types/vault';

const vaultService = {
  getCategories: async (): Promise<ApiResponse<VaultCategory[]>> => {
    const response = await api.get<ApiResponse<VaultCategory[]>>('/vault/categories');
    return response.data;
  },

  getItems: async (): Promise<ApiResponse<VaultItem[]>> => {
    const response = await api.get<ApiResponse<VaultItem[]>>('/vault/items');
    return response.data;
  },

  createItem: async (payload: CreateVaultItemPayload): Promise<ApiResponse<VaultItem>> => {
    const response = await api.post<ApiResponse<VaultItem>>('/vault/items', payload);
    return response.data;
  },

  deleteItem: async (id: number): Promise<ApiResponse<boolean>> => {
    const response = await api.delete<ApiResponse<boolean>>(`/vault/items/${id}`);
    return response.data;
  },

  setPin: async (payload: SetVaultPinPayload): Promise<ApiResponse<boolean>> => {
    const response = await api.post<ApiResponse<boolean>>('/vault/security/pin', payload);
    return response.data;
  },

  requestRevealCode: async (): Promise<ApiResponse<boolean>> => {
    const response = await api.post<ApiResponse<boolean>>('/vault/security/request-code', {});
    return response.data;
  },

  revealItem: async (id: number, payload: RevealVaultItemPayload): Promise<ApiResponse<RevealedVaultItem>> => {
    const response = await api.post<ApiResponse<RevealedVaultItem>>(`/vault/items/${id}/reveal`, payload);
    return response.data;
  },
};

export default vaultService;
