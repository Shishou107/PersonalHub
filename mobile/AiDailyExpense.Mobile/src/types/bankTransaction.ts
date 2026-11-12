export interface BankTransaction {
  bankTransactionId: number;
  userId: string;
  bankName?: string;
  accountLabel?: string;
  transactionType: 'Income' | 'Expense' | 'Transfer' | 'Unknown';
  amount: number;
  currency: string;
  description?: string;
  categoryId?: number;
  categoryName?: string;
  transactionTime?: string;
  source: string;
  rawMessage?: string;
  isConfirmed: boolean;
  createdAt: string;
}

export interface ConfirmBankTransactionPayload {
  categoryId?: number;
}
