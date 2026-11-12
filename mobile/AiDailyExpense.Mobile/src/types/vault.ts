export interface VaultCategory {
  vaultCategoryId?: number;
  name: string;
  icon?: string;
  color?: string;
}

export interface VaultItem {
  vaultItemId: number;
  vaultCategoryId: number;
  categoryName?: string;
  title: string;
  websiteUrl?: string;
  loginUsername?: string;
  loginEmail?: string;
  favorite: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateVaultItemPayload {
  vaultCategoryId?: number;
  title: string;
  websiteUrl?: string;
  loginUsername?: string;
  loginEmail?: string;
  password?: string;
  notes?: string;
  favorite: boolean;
}

export interface SetVaultPinPayload {
  oldPin?: string;
  pin: string;
  confirmPin: string;
}

export interface RevealVaultItemPayload {
  emailCode: string;
  pin: string;
}

export interface RevealedVaultItem {
  vaultItemId: number;
  password: string;
  notes?: string;
}
