export interface User {
  userId: string;
  fullName: string;
  email: string;
  cashBalance: number;
  bankBalance: number;
  totalBalance: number;
  hasInitializedBalance: boolean;
  hasVaultPin: boolean;
}

export interface AuthData {
  token: string;
  user: User;
}
